using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Logging;

public class EmailService
{
    private readonly ILogger _logger;

    // Garantiza que solo UN proceso PowerShell modifique el registro ODBC a la vez,
    // aunque EmailQueueService y MultiBaseSAPCDCService corran en paralelo.
    private static readonly SemaphoreSlim _pdfSemaphore = new(1, 1);

    private static readonly Dictionary<string, string> TiposDocumento = new()
    {
        { "1", "Factura Electrónica" },
        { "4", "Autofactura Electrónica" },
        { "5", "Nota de Crédito Electrónica" },
        { "6", "Nota de Débito Electrónica" },
        { "7", "Nota de Remisión Electrónica" }
    };

    public EmailService(ILogger logger) => _logger = logger;

    // ─────────────────────────────────────────────────────────────────────────
    // Envía el email con XML y PDF adjuntos.
    // ─────────────────────────────────────────────────────────────────────────
    public async Task EnviarAsync(ConfigCorreo config, EmailQueueItem item,
        string? rutaPdf, string cuerpoHtml, string? asunto = null)
    {
        string descTipo  = TiposDocumento.TryGetValue(item.TipoDocumento, out var d)
                           ? d : $"Documento tipo {item.TipoDocumento}";
        string numeroDoc = item.Cdc.Length >= 17 ? item.Cdc.Substring(3, 14) : item.Cdc;

        string fromEmail = (config.Correo ?? "")
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.Trim())
            .FirstOrDefault(e => !string.IsNullOrEmpty(e)) ?? "";

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Facturación Electrónica", fromEmail));

        string nombreLimpio = (item.NombreReceptor ?? "")
            .Replace(",", "").Replace(";", "").Replace("<", "")
            .Replace(">", "").Replace("\"", "").Trim();

        int agregados = 0;
        foreach (var em in (item.EmailReceptor ?? "")
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.Trim()).Where(e => !string.IsNullOrEmpty(e)))
        {
            try   { message.To.Add(new MailboxAddress(nombreLimpio, em)); agregados++; }
            catch { _logger.LogWarning($"Email destinatario inválido omitido: '{em}'"); }
        }
        if (agregados == 0)
            throw new InvalidOperationException(
                $"Ningún email válido en EMAIL_RECEPTOR: '{item.EmailReceptor}'");

        message.Subject = !string.IsNullOrWhiteSpace(asunto)
            ? asunto : $"{descTipo} autorizada - N° {numeroDoc}";

        var builder = new BodyBuilder { HtmlBody = cuerpoHtml };

        if (!string.IsNullOrWhiteSpace(item.RutaXml) && File.Exists(item.RutaXml))
            await builder.Attachments.AddAsync(item.RutaXml);
        else
            _logger.LogWarning($"XML no encontrado para adjuntar: {item.RutaXml}");

        if (!string.IsNullOrWhiteSpace(rutaPdf) && File.Exists(rutaPdf))
            await builder.Attachments.AddAsync(rutaPdf);

        message.Body = builder.ToMessageBody();

        using var smtp = new SmtpClient();

        // Puerto 465 = SSL implícito; 587 = STARTTLS; otro sin SSL = None
        var socketOptions = config.Puerto == 465
            ? SecureSocketOptions.SslOnConnect
            : config.Ssl ? SecureSocketOptions.StartTls : SecureSocketOptions.None;

        await smtp.ConnectAsync(config.Smtp, config.Puerto, socketOptions);
        string smtpUser = !string.IsNullOrWhiteSpace(config.SmtpUser) ? config.SmtpUser : fromEmail;
        await smtp.AuthenticateAsync(smtpUser, config.Password);
        await smtp.SendAsync(message);
        await smtp.DisconnectAsync(true);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Genera PDF desde un .rpt usando Crystal Reports vía PowerShell 32-bit.
    //
    // CÓMO FUNCIONA LA CONEXIÓN:
    // El .rpt fue diseñado con el driver B1CRHPROXY32 (SAP B1 Crystal Reports Proxy)
    // con DATABASE=<empresa_del_desarrollador> hardcodeado. Desde un servicio externo
    // (fuera del contexto de sesión SAP B1), B1CRHPROXY32 no establece el schema
    // correcto y siempre produce PDFs vacíos.
    //
    // SOLUCIÓN: sobreescribir en runtime la propiedad QE_LogonProperties["Connection String"]
    // del reporte con "DSN=<empresa>;UID=...;PWD=..." para usar el driver HANA ODBC
    // estándar (libodbcHDB32) directamente. Cada empresa necesita un System DSN 32-bit
    // creado en SysWOW64\odbcad32.exe con nombre = CompanyDB y CURRENTSCHEMA = CompanyDB
    // configurado en "Special property settings".
    //
    // REQUISITO: System DSN 32-bit con nombre exacto = CompanyDB (ej: CONJUNTO_ALTAVIDALUQUE)
    // creado via C:\Windows\SysWOW64\odbcad32.exe > System DSN > SAP HANA > CURRENTSCHEMA.
    // ─────────────────────────────────────────────────────────────────────────
    public static string? GenerarPdfDesdeRpt(string rutaRpt, int docEntry, ILogger logger,
        string? hanaUser = null, string? hanaPassword = null,
        string? companyDb = null, string? hanaOdbcDsn = null)
    {
        string uid     = Guid.NewGuid().ToString("N");
        string scrPath = Path.Combine(Path.GetTempPath(), $"cr_{docEntry}_{uid}.ps1");
        string pdfPath = Path.Combine(Path.GetTempPath(), $"rpt_{docEntry}_{uid}.pdf");

        // DSN = nombre de la empresa (mismo nombre que CompanyDB).
        // hanaOdbcDsn se ignora — queda en firma solo por compatibilidad.
        string dsnName = !string.IsNullOrWhiteSpace(companyDb) ? companyDb : "HANA_FE";

        _pdfSemaphore.Wait();
        try
        {
            // Escapar comillas simples para embeber en strings PS
            string usr    = (hanaUser     ?? "").Replace("'", "''");
            string pwd    = (hanaPassword ?? "").Replace("'", "''");
            string dsn    = dsnName.Replace("'", "''");
            string rpt    = rutaRpt.Replace("'", "''");
            string outPdf = pdfPath.Replace("'", "''");

            var script = new StringBuilder();
            script.AppendLine("$ErrorActionPreference = 'Stop'");
            script.AppendLine("");
            script.AppendLine("# 1) Cargar assemblies Crystal Reports (GAC 32-bit)");
            script.AppendLine("$gac = 'C:\\Windows\\assembly\\GAC_MSIL'");
            script.AppendLine("$sd  = Get-ChildItem \"$gac\\CrystalDecisions.Shared\"                | Sort-Object Name -Desc | Select-Object -First 1 -Expand FullName");
            script.AppendLine("$ed  = Get-ChildItem \"$gac\\CrystalDecisions.CrystalReports.Engine\" | Sort-Object Name -Desc | Select-Object -First 1 -Expand FullName");
            script.AppendLine("Add-Type -Path (Join-Path $sd 'CrystalDecisions.Shared.dll')");
            script.AppendLine("Add-Type -Path (Join-Path $ed 'CrystalDecisions.CrystalReports.Engine.dll')");
            script.AppendLine("");
            script.AppendLine("# 2) Cargar el reporte");
            script.AppendLine("$rpt = New-Object CrystalDecisions.CrystalReports.Engine.ReportDocument");
            script.AppendLine($"$rpt.Load('{rpt}')");
            script.AppendLine($"Write-Host '[CR] Reporte cargado: {rpt}'");
            script.AppendLine("");
            // ── Función que sobreescribe el Connection String del reporte ──
            // Cambia DRIVER={B1CRHPROXY32};...;DATABASE=<dev_empresa>
            // por     DSN=<companyDb>;UID=...;PWD=...
            // El DSN usa el driver HANA estándar (libodbcHDB32) con CURRENTSCHEMA=companyDb
            // configurado via odbcad32.exe, lo que hace que CURRENT_SCHEMA sea correcto.
            script.AppendLine("# 3) Función para sobreescribir conexión en cada tabla del reporte");
            script.AppendLine("function Set-CrConn($r, $dsn, $user, $pass) {");
            script.AppendLine("    foreach ($t in $r.Database.Tables) {");
            script.AppendLine("        try {");
            script.AppendLine("            $li    = $t.LogOnInfo");
            script.AppendLine("            $outer = $li.ConnectionInfo.Attributes.Collection");
            script.AppendLine("            $qlp   = $outer | Where-Object { $_.Name -eq 'QE_LogonProperties' }");
            script.AppendLine("            if ($qlp -and $qlp.Value -is [CrystalDecisions.Shared.DbConnectionAttributes]) {");
            script.AppendLine("                $inner = $qlp.Value.Collection");
            script.AppendLine("                $cs  = $inner | Where-Object { $_.Name -eq 'Connection String' }");
            script.AppendLine("                $srv = $inner | Where-Object { $_.Name -eq 'Server' }");
            script.AppendLine("                $uds = $inner | Where-Object { $_.Name -eq 'UseDSNProperties' }");
            script.AppendLine("                if ($cs)  { $cs.Value  = \"DSN=$dsn;UID=$user;PWD=$pass\" }");
            script.AppendLine("                if ($srv) { $srv.Value = (Get-ItemProperty \"HKLM:\\SOFTWARE\\WOW6432Node\\ODBC\\ODBC.INI\\$dsn\" -EA SilentlyContinue).ServerNode }");
            script.AppendLine("                if ($uds) { $uds.Value = 'True' }");
            script.AppendLine("            }");
            script.AppendLine("            $li.ConnectionInfo.AllowCustomConnection = $true");
            script.AppendLine("            $li.ConnectionInfo.ServerName = $dsn");
            script.AppendLine("            $li.ConnectionInfo.UserID     = $user");
            script.AppendLine("            $li.ConnectionInfo.Password   = $pass");
            script.AppendLine("            $t.ApplyLogOnInfo($li)");
            script.AppendLine("            Write-Host \"[CONN] Tabla '$($t.Name)' -> DSN=$dsn\"");
            script.AppendLine("        } catch { Write-Host \"[WARN] $($t.Name): $_\" }");
            script.AppendLine("    }");
            script.AppendLine("}");
            script.AppendLine("");
            script.AppendLine("# 4) Aplicar conexión al reporte principal y subreportes");
            script.AppendLine($"Set-CrConn $rpt '{dsn}' '{usr}' '{pwd}'");
            script.AppendLine("foreach ($sec in $rpt.ReportDefinition.Sections) {");
            script.AppendLine("    foreach ($obj in $sec.ReportObjects) {");
            script.AppendLine($"        try {{ $sub = $rpt.OpenSubreport($obj.SubreportName); Set-CrConn $sub '{dsn}' '{usr}' '{pwd}' }} catch {{}}");
            script.AppendLine("    }");
            script.AppendLine("}");
            script.AppendLine("");
            script.AppendLine("# 5) Pasar parámetro DocEntry");
            script.AppendLine($"$docVal = [double]{docEntry}");
            script.AppendLine("foreach ($pf in $rpt.DataDefinition.ParameterFields) {");
            script.AppendLine("    try {");
            script.AppendLine("        $pv = New-Object CrystalDecisions.Shared.ParameterValues");
            script.AppendLine("        $pd = New-Object CrystalDecisions.Shared.ParameterDiscreteValue");
            script.AppendLine("        $pd.Value = $docVal; [void]$pv.Add($pd)");
            script.AppendLine("        $pf.ApplyCurrentValues($pv)");
            script.AppendLine("    } catch {}");
            script.AppendLine("}");
            script.AppendLine("try { $rpt.SetParameterValue('docKey@', $docVal) } catch {}");
            script.AppendLine($"Write-Host '[CR] Parametro docKey = {docEntry}'");
            script.AppendLine("");
            script.AppendLine("# 6) Exportar a PDF");
            script.AppendLine("$opts  = New-Object CrystalDecisions.Shared.ExportOptions");
            script.AppendLine("$opts.ExportDestinationType = [CrystalDecisions.Shared.ExportDestinationType]::DiskFile");
            script.AppendLine("$opts.ExportFormatType      = [CrystalDecisions.Shared.ExportFormatType]::PortableDocFormat");
            script.AppendLine("$do    = New-Object CrystalDecisions.Shared.DiskFileDestinationOptions");
            script.AppendLine($"$do.DiskFileName = '{outPdf}'");
            script.AppendLine("$opts.DestinationOptions = $do");
            script.AppendLine("$rpt.Export($opts)");
            script.AppendLine("$rpt.Close()");
            script.AppendLine($"Write-Host '[PDF] Generado correctamente: {outPdf}'");

            File.WriteAllText(scrPath, script.ToString(), Encoding.UTF8);

            string ps32 = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "SysWOW64", "WindowsPowerShell", "v1.0", "powershell.exe");
            string psExe = File.Exists(ps32) ? ps32 : "powershell.exe";

            var psi = new ProcessStartInfo
            {
                FileName               = psExe,
                Arguments              = $"-NonInteractive -NoProfile -ExecutionPolicy Bypass -File \"{scrPath}\"",
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow         = true
            };

            using var proc    = Process.Start(psi)!;
            var stdoutTask    = Task.Run(() => proc.StandardOutput.ReadToEnd());
            var stderrTask    = Task.Run(() => proc.StandardError.ReadToEnd());
            bool termino      = proc.WaitForExit(90_000);
            string stdout     = stdoutTask.Result.Trim();
            string stderr     = stderrTask.Result.Trim();

            if (!string.IsNullOrWhiteSpace(stdout))
                logger.LogInformation($"[CR] DocEntry {docEntry} ({companyDb}): {stdout}");

            if (!termino || proc.ExitCode != 0 || !string.IsNullOrEmpty(stderr))
            {
                string err = string.Join(" | ", stderr.Split('\n')
                    .Where(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith("+"))
                    .Select(l => l.Trim()));
                logger.LogWarning($"PDF fallido DocEntry {docEntry} ({companyDb}): {err}");
                return null;
            }

            return File.Exists(pdfPath) ? pdfPath : null;
        }
        catch (Exception ex)
        {
            logger.LogWarning($"Error generando PDF DocEntry {docEntry} ({companyDb}): {ex.Message}");
            return null;
        }
        finally
        {
            _pdfSemaphore.Release();
            if (File.Exists(scrPath)) try { File.Delete(scrPath); } catch { }
        }
    }

    public static string DesencriptarContrasena(string encriptado)
    {
        if (string.IsNullOrEmpty(encriptado)) return string.Empty;
        try
        {
            using var aes = Aes.Create();
            using var sha = SHA256.Create();
            using var md5 = MD5.Create();
            aes.Key = sha.ComputeHash(Encoding.UTF8.GetBytes("FE_SAP_EmailConfig_SecretKey_2024!"));
            aes.IV  = md5.ComputeHash(Encoding.UTF8.GetBytes("FE_SAP_IV_Key!16"));
            var dec   = aes.CreateDecryptor();
            var bytes = Convert.FromBase64String(encriptado);
            return Encoding.UTF8.GetString(dec.TransformFinalBlock(bytes, 0, bytes.Length));
        }
        catch { return string.Empty; }
    }
}

public class ConfigCorreo
{
    public string Correo    { get; set; } = "";
    public string SmtpUser  { get; set; } = "";   // usuario SMTP si difiere del correo
    public string Smtp      { get; set; } = "";
    public int    Puerto    { get; set; } = 587;
    public string Password  { get; set; } = "";
    public bool   Ssl       { get; set; } = true;
    public string TplFac    { get; set; } = "";
    public string TplNC     { get; set; } = "";
    public string RptFac    { get; set; } = "";
    public string RptNC     { get; set; } = "";
}
