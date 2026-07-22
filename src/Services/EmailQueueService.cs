using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

public class EmailQueueService : BackgroundService
{
    private readonly ILogger<EmailQueueService> _logger;
    private readonly Config _config;
    private readonly EmailQueueRepository _queue;
    private readonly EmailService _emailSender;

    private const int MaxIntentos  = 3;
    private const int LotePorCiclo = 2;   // bajado de 5 → menos PATCH concurrentes a SL

    private static readonly TimeSpan IntervaloEjecucion = TimeSpan.FromSeconds(60);   // subido de 30s → menos colisión con CDC service
    private static readonly TimeSpan DelayEntreEnvios = TimeSpan.FromSeconds(3);      // subido de 2s → más respiro entre PDFs
    private static readonly TimeSpan ConfigTtl = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan SapSessionTtl = TimeSpan.FromMinutes(25);

    private readonly Dictionary<string, (ConfigCorreo? cfg, DateTime cargada)>     _configCache = new();
    private readonly Dictionary<string, (SAPServiceLayer sap, DateTime loginTime)> _sapCache    = new();

    public EmailQueueService(
        ILogger<EmailQueueService> logger,
        ILogger<EmailService> emailLogger,
        Config config,
        EmailQueueRepository queue)
    {
        _logger = logger;
        _config = config;
        _queue = queue;
        _emailSender = new EmailService(emailLogger);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EmailQueueService iniciado.");

        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var sapConfig in _config.SapServiceLayerList)
            {
                try
                {
                    await ProcesarPendientesAsync(sapConfig, stoppingToken);
                    await ProcesarSapUpdatesPendientesAsync(sapConfig, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error procesando cola de emails para {sapConfig.CompanyDB}");
                }
            }

            await Task.Delay(IntervaloEjecucion, stoppingToken);
        }
    }

    private async Task ProcesarPendientesAsync(SapServiceLayerConfig sapConfig, CancellationToken ct)
    {
        // Sin filtro de fecha: procesar todos los PENDIENTE con INTENTOS < MaxIntentos sin importar
        // cuándo se crearon. Evita que un backlog (servicio caído un día) quede atascado para siempre.
        var pendientes = _queue.ObtenerPendientes(sapConfig.CompanyDB, LotePorCiclo, MaxIntentos);
        if (pendientes.Count == 0) return;

        var sapService = await ObtenerSapService(sapConfig);
        if (sapService == null)
        {
            _logger.LogWarning($"No se pudo conectar a SAP para {sapConfig.CompanyDB}. Se omite el ciclo de email.");
            return;
        }

        var cfg = await ObtenerConfigCorreo(sapConfig, sapService);
        if (cfg == null)
        {
            _logger.LogDebug($"Sin configuración de correo para {sapConfig.CompanyDB}.");
            return;
        }

        _logger.LogInformation($"Enviando {pendientes.Count} email(s) pendiente(s) para {sapConfig.CompanyDB}");

        foreach (var item in pendientes)
        {
            if (ct.IsCancellationRequested) break;

            // Tipo SIFEN derivado del CDC (primeros 2 chars). Más confiable que TIPO_DOCUMENTO de la BD.
            string tipoReal = item.Cdc.Length >= 2 && int.TryParse(item.Cdc.Substring(0, 2), out int t)
                ? t.ToString()
                : item.TipoDocumento;
            item.TipoDocumento = tipoReal;

            bool esFactura = tipoReal == "1" || tipoReal == "4";
            string? tplPath = esFactura ? cfg.TplFac : cfg.TplNC;
            string? rptPath = esFactura ? cfg.RptFac : cfg.RptNC;

            await ProcesarItemAsync(cfg, item, tplPath, rptPath, sapService, sapConfig);
            await Task.Delay(DelayEntreEnvios, ct);
        }
    }

    // Retry de PATCH a SAP para emails que YA se enviaron al cliente pero SAP no se actualizó.
    // Esto pasa cuando el SL está caído, sesión expirada, network blip, etc.
    // Si no se reintenta, OINV.U_FE_MailEnviado queda vacío para esos docs.
    private async Task ProcesarSapUpdatesPendientesAsync(SapServiceLayerConfig sapConfig, CancellationToken ct)
    {
        const int MaxSapIntentos = 10;
        const int LoteSapUpdate  = 20;

        var pendientes = _queue.ObtenerSapUpdatesPendientes(sapConfig.CompanyDB, LoteSapUpdate, MaxSapIntentos);
        if (pendientes.Count == 0) return;

        var sapService = await ObtenerSapService(sapConfig);
        if (sapService == null) return;

        _logger.LogInformation($"Retry SAP update: {pendientes.Count} doc(s) en {sapConfig.CompanyDB}");

        foreach (var item in pendientes)
        {
            if (ct.IsCancellationRequested) break;
            // Derivar tipo desde CDC (igual que en el flujo principal)
            string tipoReal = item.Cdc.Length >= 2 && int.TryParse(item.Cdc.Substring(0, 2), out int t)
                ? t.ToString()
                : item.TipoDocumento;

            var (ok, err) = await ActualizarMailSAP(sapService, item.DocEntry, tipoReal,
                enviado: true, error: null, sapConfig);
            _queue.MarcarSapActualizado(item.Id, ok, err);
            if (ok)
                _logger.LogInformation($"[{item.BaseDatos}] SAP actualizado tarde para DocEntry {item.DocEntry} (CDC {item.Cdc})");
        }
    }

    private async Task ProcesarItemAsync(ConfigCorreo cfg, EmailQueueItem item,
        string? tplPath, string? rptPath, SAPServiceLayer sapService, SapServiceLayerConfig sapConfig)
    {
        item.RutaXml = ResolverRutaXml(item, _logger);

        string? rutaPdf = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(rptPath) && File.Exists(rptPath))
            {
                _logger.LogInformation($"[{item.BaseDatos}] Generando PDF DocEntry={item.DocEntry} (CDC={item.Cdc})");
                rutaPdf = EmailService.GenerarPdfDesdeRpt(
                    rptPath, item.DocEntry, _logger,
                    _config.HanaDatabase?.UserName,
                    _config.HanaDatabase?.Password,
                    item.BaseDatos,
                    _config.HanaDatabase?.OdbcDsn);

                // 1) PDF debe existir
                if (string.IsNullOrWhiteSpace(rutaPdf) || !File.Exists(rutaPdf))
                {
                    throw new InvalidOperationException(
                        $"No se pudo generar el PDF para DocEntry={item.DocEntry}. " +
                        $"El correo NO se envía hasta que el PDF se genere correctamente.");
                }

                // 2) PDF debe tener tamaño razonable. Un PDF "vacío" (solo template + subreporte
                // de actividades, sin datos del documento) pesa ~33KB. Umbral conservador: 40KB.
                // Por debajo de eso, claramente el CURRENTSCHEMA no propagó o el doc no existe.
                long pdfSize = new FileInfo(rutaPdf).Length;
                _logger.LogInformation($"[{item.BaseDatos}] PDF DocEntry={item.DocEntry} tamaño={pdfSize} bytes");

                const long PDF_MIN_BYTES = 40_000;
                if (pdfSize < PDF_MIN_BYTES)
                {
                    throw new InvalidOperationException(
                        $"PDF DocEntry={item.DocEntry} parece VACÍO ({pdfSize} bytes < {PDF_MIN_BYTES}). " +
                        $"Probable: CURRENTSCHEMA={item.BaseDatos} no se propaga al SQL Command del .rpt, " +
                        $"o el documento no existe en ese schema. El correo NO se envía.");
                }
            }

            DateTime? docDueDate = await ObtenerDocDueDate(sapService, item.DocEntry, item.TipoDocumento);
            var (asunto, cuerpoHtml) = ObtenerCuerpo(tplPath, item, docDueDate);

            await _emailSender.EnviarAsync(cfg, item, rutaPdf, cuerpoHtml, asunto);
            _queue.MarcarEnviado(item.Id);
            _logger.LogInformation($"[{item.BaseDatos}] Email enviado para CDC {item.Cdc} → {item.EmailReceptor}");

            var (sapOk, sapErr) = await ActualizarMailSAP(sapService, item.DocEntry, item.TipoDocumento, enviado: true, error: null, sapConfig);
            _queue.MarcarSapActualizado(item.Id, sapOk, sapErr);
            if (!sapOk)
                _logger.LogInformation($"[{item.BaseDatos}] Email YA ENVIADO al cliente pero SAP no se pudo actualizar para DocEntry {item.DocEntry}. Quedará en cola para retry de SAP.");
        }
        catch (Exception ex)
        {
            _logger.LogError($"[{item.BaseDatos}] Error al enviar email para CDC {item.Cdc}: {ex.Message}");
            _queue.MarcarError(item.Id, ex.Message, item.Intentos, MaxIntentos);
            // No marcar SAP_ACTUALIZADO aquí — el email NO se envió, no tiene sentido tocar OINV
            await ActualizarMailSAP(sapService, item.DocEntry, item.TipoDocumento, enviado: false, error: ex.Message, sapConfig);
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(rutaPdf) && File.Exists(rutaPdf))
                try { File.Delete(rutaPdf); } catch { }
        }
    }

    // Obtiene DocDueDate (fecha de vencimiento) del documento en SAP.
    private async Task<DateTime?> ObtenerDocDueDate(SAPServiceLayer sapService, int docEntry, string tipoDocumento)
    {
        if (tipoDocumento != "1" && tipoDocumento != "4" &&
            tipoDocumento != "5" && tipoDocumento != "6") return null;

        try
        {
            string endpoint = (tipoDocumento == "1" || tipoDocumento == "4")
                ? $"Invoices({docEntry})?$select=DocDueDate"
                : $"CreditNotes({docEntry})?$select=DocDueDate";
            var response = await sapService.GetHttpClient().GetAsync(endpoint);
            if (!response.IsSuccessStatusCode) return null;
            var json = await response.Content.ReadAsStringAsync();
            var obj  = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
            if (obj != null && obj.TryGetValue("DocDueDate", out var v) && v != null
                && DateTime.TryParse(v.ToString(), out DateTime fecha))
            {
                return fecha;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug($"No se pudo obtener DocDueDate para DocEntry {docEntry}: {ex.Message}");
        }
        return null;
    }

    // Actualiza U_FE_MailEnviado y U_FE_MailError en SAP.
    // Solo aplica para Facturas (1/4 → Invoices) y Notas de Crédito (5/6 → CreditNotes).
    // Retorna true si el PATCH fue exitoso, false si falló (caller decide qué hacer).
    private async Task<(bool ok, string? error)> ActualizarMailSAP(SAPServiceLayer sapService, int docEntry,
        string tipoDocumento, bool enviado, string? error,
        SapServiceLayerConfig? sapConfig = null)
    {
        if (tipoDocumento != "1" && tipoDocumento != "4" &&
            tipoDocumento != "5" && tipoDocumento != "6")
            return (true, null);  // tipo no aplica, no es error

        string mensajeError = enviado
            ? "Enviado"
            : (error?.Length > 200 ? error[..200] : error) ?? "";

        var body = new
        {
            U_FE_MailEnviado = enviado ? "SI" : "NO",
            U_FE_MailError   = mensajeError
        };

        string endpoint = (tipoDocumento == "1" || tipoDocumento == "4")
            ? $"Invoices({docEntry})"
            : $"CreditNotes({docEntry})";

        var (ok, errMsg) = await IntentarPatch(sapService, endpoint, body, docEntry, "intento 1");

        // Si falló y tenemos config, refrescar sesión y reintentar UNA vez
        if (!ok && sapConfig != null)
        {
            _logger.LogInformation($"Refrescando sesión SAP para reintentar PATCH DocEntry {docEntry}");
            _sapCache.Remove(sapConfig.CompanyDB);
            var sapNuevo = await ObtenerSapService(sapConfig);
            if (sapNuevo != null)
                (ok, errMsg) = await IntentarPatch(sapNuevo, endpoint, body, docEntry, "intento 2 con sesión fresca");
        }

        return (ok, errMsg);
    }

    private async Task<(bool ok, string? error)> IntentarPatch(SAPServiceLayer sap, string endpoint, object body, int docEntry, string contexto)
    {
        try
        {
            var content  = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
            var response = await sap.GetHttpClient().PatchAsync(endpoint, content);
            if (response.IsSuccessStatusCode) return (true, null);
            string respBody = await response.Content.ReadAsStringAsync();
            string err = $"HTTP {(int)response.StatusCode} {response.StatusCode} | {respBody.Substring(0, Math.Min(300, respBody.Length))}";
            _logger.LogWarning($"PATCH SAP fallido ({contexto}) DocEntry {docEntry}: {err}");
            return (false, err);
        }
        catch (Exception ex)
        {
            string err = ex.Message + (ex.InnerException != null ? $" | INNER: {ex.InnerException.Message}" : "");
            _logger.LogWarning($"PATCH SAP excepción ({contexto}) DocEntry {docEntry}: {err}");
            return (false, err);
        }
    }

    // Procesa la plantilla y retorna (asunto, cuerpoHtml).
    // Formato de plantilla: primera línea "ASUNTO: ..." + separador "---" + HTML.
    private (string asunto, string cuerpo) ObtenerCuerpo(string? tplPath, EmailQueueItem item, DateTime? docDueDate = null)
    {
        string contenido = "";
        if (!string.IsNullOrWhiteSpace(tplPath) && File.Exists(tplPath))
            try { contenido = File.ReadAllText(tplPath, Encoding.UTF8); } catch { }

        string asunto    = "";
        string plantilla = contenido;

        if (contenido.TrimStart().StartsWith("ASUNTO:", StringComparison.OrdinalIgnoreCase))
        {
            var lineas = contenido.Split('\n');
            asunto = lineas[0].Replace("ASUNTO:", "", StringComparison.OrdinalIgnoreCase).Trim();
            int sepIdx = Array.FindIndex(lineas, 1, l => l.Trim() == "---");
            plantilla = sepIdx >= 0
                ? string.Join("\n", lineas[(sepIdx + 1)..])
                : string.Join("\n", lineas[1..]);
        }

        if (string.IsNullOrWhiteSpace(plantilla))
            plantilla = PlantillaHtmlPorDefecto();

        var x = LeerCamposXml(item.RutaXml);
        string V(string tag) => x.TryGetValue(tag, out var v) ? v : "";

        // Número formateado: dEst-dPunExp-dNumDoc desde el XML, con fallback al CDC
        // CDC: 2(tipo) + 8(RUC) + 1(DV) + 3(EST) + 3(PTO) + 7(NUMDOC) + ...
        string nroDocFallback = item.Cdc.Length >= 24
            ? $"{item.Cdc.Substring(11, 3)}-{item.Cdc.Substring(14, 3)}-{item.Cdc.Substring(17, 7)}"
            : item.Cdc;
        string est = V("dEst"), pto = V("dPunExp"), num = V("dNumDoc");
        string nroDoc = (!string.IsNullOrEmpty(est) && !string.IsNullOrEmpty(pto) && !string.IsNullOrEmpty(num))
            ? $"{est}-{pto}-{num}"
            : nroDocFallback;

        // RUC del receptor (camelCase: dRucRec) combinado con DV: ej "80150283-7".
        // Si no hay RUC, usa el documento de identidad (dNumIDRec) para no-contribuyentes.
        string ruc   = V("dRucRec");
        string dv    = V("dDVRec");
        string rucCliente = !string.IsNullOrEmpty(ruc)
            ? (!string.IsNullOrEmpty(dv) ? $"{ruc}-{dv}" : ruc)
            : V("dNumIDRec");

        // Fecha emisión: parsear ISO 8601 ("2026-06-01T10:18:00") y mostrar solo fecha
        string fechaEmision = DateTime.TryParse(V("dFeEmiDE"), out DateTime feEmi)
            ? feEmi.ToString("dd/MM/yyyy")
            : DateTime.Now.ToString("dd/MM/yyyy");

        // Moneda: código operación (PYG/USD) o descripción como fallback
        string moneda = !string.IsNullOrEmpty(V("cMoneOpe")) ? V("cMoneOpe") : V("dDesMon");

        // Total: formato Paraguay (miles con ".", decimal con ","). PYG sin decimales.
        var cultPy = System.Globalization.CultureInfo.GetCultureInfo("es-PY");
        bool esGuarani = moneda.Equals("PYG", StringComparison.OrdinalIgnoreCase) ||
                         moneda.Equals("GS",  StringComparison.OrdinalIgnoreCase);
        string totalRaw = V("dTotGralOpe");
        string total = decimal.TryParse(totalRaw, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out decimal totalDec)
            ? totalDec.ToString(esGuarani ? "N0" : "N2", cultPy)
            : totalRaw;

        // Condición de pago: descripción explícita o derivada del código
        string condPago = !string.IsNullOrEmpty(V("dDesCCondOpe"))
            ? V("dDesCCondOpe")
            : V("iCondOpe") switch { "1" => "Contado", "2" => "Crédito", _ => "" };

        // Fecha vencimiento: fuente primaria OINV.DocDueDate (ya calculada por SAP).
        // Si SAP no la tiene, fallback a dVencCuo del XML (crédito por cuotas).
        string fechaVen = docDueDate.HasValue
            ? docDueDate.Value.ToString("dd/MM/yyyy")
            : V("dVencCuo");

        string nombreCliente = !string.IsNullOrEmpty(item.NombreReceptor)
            ? item.NombreReceptor
            : V("dNomRec");

        // Para NC: factura referenciada + fecha referencia.
        //   Si la factura ref es ELECTRÓNICA → viene en dCdCDERef (CDC de 44 chars).
        //     Del CDC extraemos: número (pos 11-23) y fecha (pos 25-32 yyyymmdd).
        //   Si la factura ref es IMPRESA → viene en dNumDocAso + dFecEmiDI.
        string facturaRef = "";
        string fechaRef   = "";
        string cdcRef     = V("dCdCDERef");
        if (!string.IsNullOrEmpty(cdcRef) && cdcRef.Length >= 33)
        {
            facturaRef = $"{cdcRef.Substring(11, 3)}-{cdcRef.Substring(14, 3)}-{cdcRef.Substring(17, 7)}";
            string fechaCdc = cdcRef.Substring(25, 8); // yyyymmdd dentro del CDC
            if (DateTime.TryParseExact(fechaCdc, "yyyyMMdd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out DateTime fr))
            {
                fechaRef = fr.ToString("dd/MM/yyyy");
            }
        }
        else
        {
            facturaRef = V("dNumDocAso");
            if (DateTime.TryParse(V("dFecEmiDI"), out DateTime fi))
                fechaRef = fi.ToString("dd/MM/yyyy");
        }

        string enlaceQr = string.IsNullOrWhiteSpace(item.Qr) ? "" :
            $"<a href=\"{Esc(item.Qr)}\" style=\"color:#0066cc;word-break:break-all;\">{Esc(item.Qr)}</a>";

        var rep = new Dictionary<string, string>
        {
            { "{NOMBRE_CLIENTE}",     Esc(nombreCliente)     },
            { "{NUMERO_DOCUMENTO}",   Esc(nroDoc)            },
            { "{FECHA}",              Esc(fechaEmision)      },
            { "{RUC_CLIENTE}",        Esc(rucCliente)        },
            { "{EMPRESA}",            Esc(V("dNomEmi"))      },
            { "{MONEDA}",             Esc(moneda)            },
            { "{TOTAL}",              Esc(total)             },
            { "{CONDICION_PAGO}",     Esc(condPago)          },
            { "{FECHA_VENCIMIENTO}",  Esc(fechaVen)          },
            { "{MOTIVO}",             Esc(V("dDesMotEmi"))   },
            { "{FACTURA_REFERENCIA}", Esc(facturaRef)        },
            { "{FECHA_FACTURA_REF}",  Esc(fechaRef)          },
            { "{TELEFONO}",           ""                     },
            { "{SITIO_WEB}",          ""                     },
            { "{QR_LINK}",            enlaceQr               },
            { "{QR_URL}",             Esc(item.Qr ?? "")     },
            { "{CDC}",                Esc(item.Cdc)          },
        };

        string cuerpo = plantilla;
        foreach (var (k, v) in rep)
        {
            cuerpo = cuerpo.Replace(k, v);
            asunto = asunto.Replace(k, v);
        }

        return (asunto, cuerpo);
    }

    // Resuelve la ruta efectiva del XML firmado.
    // Si el path guardado existe se usa; si no, se reconstruye con BaseDirectory actual
    // (necesario cuando el registro fue creado por un proceso con diferente directorio base).
    private static string ResolverRutaXml(EmailQueueItem item, ILogger logger)
    {
        if (!string.IsNullOrWhiteSpace(item.RutaXml) && File.Exists(item.RutaXml))
            return item.RutaXml;

        string nombreArchivo = !string.IsNullOrWhiteSpace(item.RutaXml)
            ? Path.GetFileName(item.RutaXml)
            : $"Documento_{item.Cdc}.xml";

        string rutaReconstruida = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "XML", item.BaseDatos, nombreArchivo);

        if (!File.Exists(rutaReconstruida))
            logger.LogWarning($"XML no encontrado ni en ruta guardada ({item.RutaXml}) ni en ruta reconstruida ({rutaReconstruida})");

        return rutaReconstruida;
    }

    // Lee campos del XML firmado SIFEN. Usa namespace wildcard "*" para resolver elementos
    // independientemente del namespace declarado.
    private static Dictionary<string, string> LeerCamposXml(string rutaXml)
    {
        var c = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(rutaXml)) return c;
        try
        {
            var xml = new XmlDocument();
            xml.Load(rutaXml);

            string? G(string tag) => xml.GetElementsByTagName(tag, "*")?[0]?.InnerText
                                  ?? xml.GetElementsByTagName(tag)?[0]?.InnerText;
            void S(string key, string? val) { if (!string.IsNullOrEmpty(val)) c[key] = val; }

            S("dNomRec",      G("dNomRec"));
            S("dNomEmi",      G("dNomEmi"));
            S("dRucEmi",      G("dRucEmi"));      // RUC emisor (camelCase SIFEN)
            S("dRucRec",      G("dRucRec"));      // RUC receptor (camelCase SIFEN)
            S("dDVRec",       G("dDVRec"));       // Dígito verificador receptor
            S("dNumIDRec",    G("dNumIDRec"));    // ID receptor (no contribuyente)
            S("dFeEmiDE",     G("dFeEmiDE"));
            S("dEst",         G("dEst"));
            S("dPunExp",      G("dPunExp"));
            S("dNumDoc",      G("dNumDoc"));
            S("dNumTim",      G("dNumTim"));
            S("dTotGralOpe",  G("dTotGralOpe"));
            S("cMoneOpe",     G("cMoneOpe"));
            S("dDesMon",      G("dDesMon"));
            S("iCondOpe",     G("iCondOpe"));
            S("dDesCCondOpe", G("dDesCCondOpe"));
            S("dVencCuo",     G("dVencCuo"));     // Vencimiento por cuota (crédito iCondCred=2)
            S("dPlazoCre",    G("dPlazoCre"));    // Plazo crédito texto, ej "30 días" (iCondCred=1)
            // NC: motivo + documento asociado (puede ser electrónico o impreso)
            S("dDesMotEmi",   G("dDesMotEmi"));   // Descripción motivo emisión NC
            S("dCdCDERef",    G("dCdCDERef"));    // CDC del DE referenciado (cuando es electrónico)
            S("dNumDocAso",   G("dNumDocAso"));   // Número doc asociado (cuando es impreso)
            S("dFecEmiDI",    G("dFecEmiDI"));    // Fecha emisión doc impreso (cuando es impreso)
        }
        catch { }
        return c;
    }

    // Devuelve un SAPServiceLayer autenticado, reutilizando la sesión por 25 minutos.
    private async Task<SAPServiceLayer?> ObtenerSapService(SapServiceLayerConfig sapConfig)
    {
        string key = sapConfig.CompanyDB;
        if (_sapCache.TryGetValue(key, out var cached) && DateTime.Now - cached.loginTime < SapSessionTtl)
            return cached.sap;

        var sap = new SAPServiceLayer(sapConfig);
        bool ok  = await sap.Login();
        if (!ok)
        {
            _logger.LogWarning($"Login SAP fallido para {sapConfig.CompanyDB}");
            return null;
        }

        _sapCache[key] = (sap, DateTime.Now);
        return sap;
    }

    private async Task<ConfigCorreo?> ObtenerConfigCorreo(SapServiceLayerConfig sapConfig, SAPServiceLayer sapService)
    {
        string key = sapConfig.CompanyDB;
        if (_configCache.TryGetValue(key, out var cached) && DateTime.Now - cached.cargada < ConfigTtl)
            return cached.cfg;

        try
        {
            var response = await sapService.GetHttpClient()
                .GetAsync($"U_CONFIG_EMAIL?$filter=U_BASE_DATOS eq '{sapConfig.CompanyDB}'");

            if (!response.IsSuccessStatusCode) { _configCache[key] = (null, DateTime.Now); return null; }

            var json = await response.Content.ReadAsStringAsync();
            var obj  = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
            if (obj == null || !obj.ContainsKey("value")) { _configCache[key] = (null, DateTime.Now); return null; }

            var lista = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(obj["value"].ToString()!);
            if (lista == null || lista.Count == 0) { _configCache[key] = (null, DateTime.Now); return null; }

            var reg    = lista[0];
            string pwd = reg.TryGetValue("U_PWD", out var pw) ? pw?.ToString() ?? "" : "";

            var cfg = new ConfigCorreo
            {
                Correo   = reg.TryGetValue("U_CORREO",  out var c)  ? c?.ToString()  ?? "" : "",
                Smtp     = reg.TryGetValue("U_SMTP",    out var s)  ? s?.ToString()  ?? "" : "",
                Puerto   = reg.TryGetValue("U_PUERTO",  out var p) && int.TryParse(p?.ToString(), out int port) ? port : 587,
                Password = EmailService.DesencriptarContrasena(pwd),
                Ssl      = reg.TryGetValue("U_SSL",     out var ssl) && ssl?.ToString() == "Y",
                TplFac   = reg.TryGetValue("U_TPL_FAC", out var tf) ? tf?.ToString() ?? "" : "",
                TplNC    = reg.TryGetValue("U_TPL_NC",  out var tn) ? tn?.ToString() ?? "" : "",
                RptFac   = reg.TryGetValue("U_RPT_FAC", out var rf) ? rf?.ToString() ?? "" : "",
                RptNC    = reg.TryGetValue("U_RPT_NC",  out var rn) ? rn?.ToString() ?? "" : ""
            };

            _configCache[key] = (cfg, DateTime.Now);
            return cfg;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error al obtener config de correo para {sapConfig.CompanyDB}: {ex.Message}");
            _configCache[key] = (null, DateTime.Now);
            return null;
        }
    }

    private static string Esc(string? v) => System.Security.SecurityElement.Escape(v ?? "") ?? "";

    private static string PlantillaHtmlPorDefecto() => @"<!DOCTYPE html>
<html><head><meta charset=""utf-8""/></head>
<body style=""font-family:Arial,sans-serif;color:#333;"">
  <table width=""600"" cellpadding=""0"" cellspacing=""0"" style=""margin:30px auto;border:1px solid #ddd;border-radius:6px;overflow:hidden;"">
    <tr><td style=""background:#004a8f;padding:20px 30px;"">
      <h2 style=""color:#fff;margin:0;"">Documento Electrónico Autorizado</h2>
    </td></tr>
    <tr><td style=""padding:30px;"">
      <p>Estimado/a <strong>{NOMBRE_CLIENTE}</strong>,</p>
      <table width=""100%"" cellpadding=""8"" cellspacing=""0"" style=""border-collapse:collapse;margin:20px 0;"">
        <tr style=""background:#f5f5f5;""><td style=""border:1px solid #ddd;""><strong>Número</strong></td><td style=""border:1px solid #ddd;"">{NUMERO_DOCUMENTO}</td></tr>
        <tr><td style=""border:1px solid #ddd;""><strong>Fecha emisión</strong></td><td style=""border:1px solid #ddd;"">{FECHA}</td></tr>
        <tr style=""background:#f5f5f5;""><td style=""border:1px solid #ddd;""><strong>RUC / ID</strong></td><td style=""border:1px solid #ddd;"">{RUC_CLIENTE}</td></tr>
        <tr><td style=""border:1px solid #ddd;""><strong>Total</strong></td><td style=""border:1px solid #ddd;"">{MONEDA} {TOTAL}</td></tr>
        <tr style=""background:#f5f5f5;""><td style=""border:1px solid #ddd;""><strong>CDC</strong></td><td style=""border:1px solid #ddd;font-size:11px;word-break:break-all;"">{CDC}</td></tr>
        <tr><td style=""border:1px solid #ddd;""><strong>Verificar</strong></td><td style=""border:1px solid #ddd;"">{QR_LINK}</td></tr>
      </table>
      <p>Adjunto encontrará el documento XML firmado y el PDF correspondiente.</p>
      <p style=""color:#888;font-size:12px;"">Este es un mensaje automático. Por favor no responda este correo.</p>
    </td></tr>
  </table>
</body></html>";
}
