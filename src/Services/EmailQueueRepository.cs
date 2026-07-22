using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Odbc;
using Microsoft.Extensions.Logging;

public class EmailQueueRepository
{
    private readonly string _connectionString;
    private readonly ILogger<EmailQueueRepository> _logger;
    private const string Schema = "SAP_SIFEN";
    private const string Table = "COLA_EMAILS";

    public EmailQueueRepository(string connectionString, ILogger<EmailQueueRepository> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public void Enqueue(string baseDatos, string cdc, int docEntry, string tipoDocumento,
        string? emailReceptor, string nombreReceptor, string? qr, string rutaXml)
    {
        string estado = string.IsNullOrWhiteSpace(emailReceptor) ? "SIN_EMAIL" : "PENDIENTE";
        try
        {
            using var conn = new OdbcConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
                INSERT INTO {Schema}.{Table}
                    (BASE_DATOS, CDC, DOC_ENTRY, TIPO_DOCUMENTO, EMAIL_RECEPTOR, NOMBRE_RECEPTOR, QR, RUTA_XML, ESTADO, INTENTOS, FECHA_CREACION)
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, 0, CURRENT_TIMESTAMP)";

            void Str(string? v) { var p = cmd.CreateParameter(); p.DbType = DbType.AnsiString; p.Value = v ?? ""; cmd.Parameters.Add(p); }
            void Int(int v)     { var p = cmd.CreateParameter(); p.DbType = DbType.Int32;      p.Value = v;       cmd.Parameters.Add(p); }

            Str(baseDatos);
            Str(cdc);
            Int(docEntry);
            Str(tipoDocumento);
            Str(emailReceptor ?? "");
            Str(nombreReceptor);
            Str(qr ?? "");
            Str(rutaXml);
            Str(estado);

            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error al encolar email para CDC {cdc}: {ex.Message}");
        }
    }

    public List<EmailQueueItem> ObtenerPendientes(string baseDatos, int limite = 5, int maxIntentos = 5, DateTime? fechaDesde = null)
    {
        var items = new List<EmailQueueItem>();
        try
        {
            using var conn = new OdbcConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();

            string filtroFecha = fechaDesde.HasValue ? " AND FECHA_CREACION >= ?" : "";
            cmd.CommandText = $@"
                SELECT TOP {limite}
                    ID, BASE_DATOS, CDC, DOC_ENTRY, TIPO_DOCUMENTO,
                    EMAIL_RECEPTOR, NOMBRE_RECEPTOR, QR, RUTA_XML, ESTADO, INTENTOS, FECHA_CREACION
                FROM {Schema}.{Table}
                WHERE BASE_DATOS = ? AND ESTADO = 'PENDIENTE' AND INTENTOS < ?{filtroFecha}
                ORDER BY FECHA_CREACION ASC";

            var p1 = cmd.CreateParameter(); p1.DbType = DbType.AnsiString; p1.Value = baseDatos;   cmd.Parameters.Add(p1);
            var p2 = cmd.CreateParameter(); p2.DbType = DbType.Int32;      p2.Value = maxIntentos; cmd.Parameters.Add(p2);
            if (fechaDesde.HasValue)
            {
                var p3 = cmd.CreateParameter(); p3.DbType = DbType.DateTime; p3.Value = fechaDesde.Value; cmd.Parameters.Add(p3);
            }

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                items.Add(new EmailQueueItem
                {
                    Id             = Convert.ToInt32(reader["ID"]),
                    BaseDatos      = reader["BASE_DATOS"]?.ToString()      ?? "",
                    Cdc            = reader["CDC"]?.ToString()             ?? "",
                    DocEntry       = Convert.ToInt32(reader["DOC_ENTRY"]),
                    TipoDocumento  = reader["TIPO_DOCUMENTO"]?.ToString()  ?? "",
                    EmailReceptor  = reader["EMAIL_RECEPTOR"]?.ToString(),
                    NombreReceptor = reader["NOMBRE_RECEPTOR"]?.ToString() ?? "",
                    Qr             = reader["QR"]?.ToString(),
                    RutaXml        = reader["RUTA_XML"]?.ToString()        ?? "",
                    Estado         = reader["ESTADO"]?.ToString()          ?? "",
                    Intentos       = Convert.ToInt32(reader["INTENTOS"]),
                    FechaCreacion  = Convert.ToDateTime(reader["FECHA_CREACION"])
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error al obtener emails pendientes de {baseDatos}: {ex.Message}");
        }
        return items;
    }

    public void MarcarEnviado(int id)
    {
        try
        {
            using var conn = new OdbcConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"UPDATE {Schema}.{Table} SET ESTADO = 'ENVIADO', FECHA_ENVIO = CURRENT_TIMESTAMP WHERE ID = ?";
            var p = cmd.CreateParameter(); p.DbType = DbType.Int32; p.Value = id; cmd.Parameters.Add(p);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error al marcar enviado ID {id}: {ex.Message}");
        }
    }

    // Marca el resultado del PATCH a SAP para este registro.
    public void MarcarSapActualizado(int id, bool ok, string? error)
    {
        try
        {
            using var conn = new OdbcConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"UPDATE {Schema}.{Table}
                SET SAP_ACTUALIZADO = ?,
                    SAP_INTENTOS    = SAP_INTENTOS + 1,
                    SAP_ULTIMO_ERROR= ?,
                    SAP_FECHA_UPDATE= CURRENT_TIMESTAMP
                WHERE ID = ?";
            void Str(string? v) { var p = cmd.CreateParameter(); p.DbType = DbType.AnsiString; p.Value = v ?? ""; cmd.Parameters.Add(p); }
            void Int(int v)     { var p = cmd.CreateParameter(); p.DbType = DbType.Int32;      p.Value = v;       cmd.Parameters.Add(p); }
            Str(ok ? "SI" : "NO");
            string err = error ?? "";
            Str(err.Length > 500 ? err[..500] : err);
            Int(id);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error al marcar SAP_ACTUALIZADO ID {id}: {ex.Message}");
        }
    }

    // Trae emails ya ENVIADO pero cuyo PATCH a SAP falló (o nunca se intentó), para retry.
    public List<EmailQueueItem> ObtenerSapUpdatesPendientes(string baseDatos, int limite = 20, int maxIntentos = 10)
    {
        var items = new List<EmailQueueItem>();
        try
        {
            using var conn = new OdbcConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
                SELECT TOP {limite}
                    ID, BASE_DATOS, CDC, DOC_ENTRY, TIPO_DOCUMENTO,
                    EMAIL_RECEPTOR, NOMBRE_RECEPTOR, QR, RUTA_XML, ESTADO, INTENTOS, FECHA_CREACION
                FROM {Schema}.{Table}
                WHERE BASE_DATOS = ?
                  AND ESTADO = 'ENVIADO'
                  AND IFNULL(SAP_ACTUALIZADO, 'NO') = 'NO'
                  AND IFNULL(SAP_INTENTOS, 0) < ?
                ORDER BY FECHA_ENVIO ASC";

            var p1 = cmd.CreateParameter(); p1.DbType = DbType.AnsiString; p1.Value = baseDatos;   cmd.Parameters.Add(p1);
            var p2 = cmd.CreateParameter(); p2.DbType = DbType.Int32;      p2.Value = maxIntentos; cmd.Parameters.Add(p2);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                items.Add(new EmailQueueItem
                {
                    Id             = Convert.ToInt32(reader["ID"]),
                    BaseDatos      = reader["BASE_DATOS"]?.ToString()      ?? "",
                    Cdc            = reader["CDC"]?.ToString()             ?? "",
                    DocEntry       = Convert.ToInt32(reader["DOC_ENTRY"]),
                    TipoDocumento  = reader["TIPO_DOCUMENTO"]?.ToString()  ?? "",
                    EmailReceptor  = reader["EMAIL_RECEPTOR"]?.ToString(),
                    NombreReceptor = reader["NOMBRE_RECEPTOR"]?.ToString() ?? "",
                    Qr             = reader["QR"]?.ToString(),
                    RutaXml        = reader["RUTA_XML"]?.ToString()        ?? "",
                    Estado         = reader["ESTADO"]?.ToString()          ?? "",
                    Intentos       = Convert.ToInt32(reader["INTENTOS"]),
                    FechaCreacion  = Convert.ToDateTime(reader["FECHA_CREACION"])
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error al obtener SAP updates pendientes de {baseDatos}: {ex.Message}");
        }
        return items;
    }

    public void MarcarError(int id, string error, int intentosActuales, int maxIntentos)
    {
        string nuevoEstado = (intentosActuales + 1) >= maxIntentos ? "ERROR" : "PENDIENTE";
        try
        {
            using var conn = new OdbcConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"UPDATE {Schema}.{Table}
                SET ESTADO = ?, INTENTOS = ?, ULTIMO_ERROR = ?
                WHERE ID = ?";

            void Str(string? v) { var p = cmd.CreateParameter(); p.DbType = DbType.AnsiString; p.Value = v ?? ""; cmd.Parameters.Add(p); }
            void Int(int v)     { var p = cmd.CreateParameter(); p.DbType = DbType.Int32;      p.Value = v;       cmd.Parameters.Add(p); }

            Str(nuevoEstado);
            Int(intentosActuales + 1);
            Str(error.Length > 2000 ? error[..2000] : error);
            Int(id);

            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error al marcar error ID {id}: {ex.Message}");
        }
    }

    // Reactiva registros en estado ERROR para reintento manual (ej: SMTP estaba caído, se corrigió).
    // Sin parámetros: reactiva todos los ERROR de esa base. Con cdc: solo ese documento.
    public void ReactivarError(string baseDatos, string? cdc = null)
    {
        try
        {
            using var conn = new OdbcConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();

            string filtroCdc = cdc != null ? " AND CDC = ?" : "";
            cmd.CommandText = $@"UPDATE {Schema}.{Table}
                SET ESTADO = 'PENDIENTE', INTENTOS = 0, ULTIMO_ERROR = NULL
                WHERE BASE_DATOS = ? AND ESTADO = 'ERROR'{filtroCdc}";

            var p1 = cmd.CreateParameter(); p1.DbType = DbType.AnsiString; p1.Value = baseDatos; cmd.Parameters.Add(p1);
            if (cdc != null)
            {
                var p2 = cmd.CreateParameter(); p2.DbType = DbType.AnsiString; p2.Value = cdc; cmd.Parameters.Add(p2);
            }

            int filas = cmd.ExecuteNonQuery();
            _logger.LogInformation($"Reactivados {filas} registro(s) ERROR para reenvío en {baseDatos}{(cdc != null ? $" / CDC {cdc}" : "")}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error al reactivar registros ERROR en {baseDatos}: {ex.Message}");
        }
    }

    // Reactiva registros SIN_EMAIL de un CDC específico cuando se actualiza el email del SN en SAP.
    // Llamar con el nuevo email para que el siguiente ciclo lo envíe.
    public void ReactivarSinEmail(string cdc, string nuevoEmail, string nuevoNombre)
    {
        try
        {
            using var conn = new OdbcConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"UPDATE {Schema}.{Table}
                SET ESTADO = 'PENDIENTE', EMAIL_RECEPTOR = ?, NOMBRE_RECEPTOR = ?, INTENTOS = 0, ULTIMO_ERROR = NULL
                WHERE CDC = ? AND ESTADO = 'SIN_EMAIL'";

            void Str(string? v) { var p = cmd.CreateParameter(); p.DbType = DbType.AnsiString; p.Value = v ?? ""; cmd.Parameters.Add(p); }
            Str(nuevoEmail);
            Str(nuevoNombre);
            Str(cdc);

            int filas = cmd.ExecuteNonQuery();
            _logger.LogInformation($"Reactivados {filas} registro(s) SIN_EMAIL para CDC {cdc}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error al reactivar SIN_EMAIL para CDC {cdc}: {ex.Message}");
        }
    }
}

public class EmailQueueItem
{
    public int      Id             { get; set; }
    public string   BaseDatos      { get; set; } = "";
    public string   Cdc            { get; set; } = "";
    public int      DocEntry       { get; set; }
    public string   TipoDocumento  { get; set; } = "";
    public string?  EmailReceptor  { get; set; }
    public string   NombreReceptor { get; set; } = "";
    public string?  Qr             { get; set; }
    public string   RutaXml        { get; set; } = "";
    public string   Estado         { get; set; } = "PENDIENTE";
    public int      Intentos       { get; set; }
    public DateTime FechaCreacion  { get; set; }
}
