using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http;
using System.Collections.Generic;
using System.Threading.Tasks;

public class EventoServiceCancelacion
{
    private readonly SAPServiceLayer _sapServiceLayer;
    private readonly HttpClient _httpClient;
    private readonly ILogger<EventoServiceCancelacion> _logger;

    public EventoServiceCancelacion(SAPServiceLayer sapServiceLayer, ILogger<EventoServiceCancelacion> logger)
    {
        _sapServiceLayer = sapServiceLayer;
        _httpClient = sapServiceLayer.GetHttpClient();
        _logger = logger;
    }

    public async Task<List<EventoCancelacion>> GetEventoFacturaCancelada()
    {
        string queryDocumento = "Invoices?$select=DocEntry,DocNum,U_EXX_FE_CDC,Comments,DocumentStatus,CancelStatus&$filter=Cancelled eq 'tYES' and U_EXX_FE_Estado eq 'AUT' AND U_EXX_FE_ANULACION_ESTADO eq 'NAU' and DocDate ge '20260601'";

        var jsonResponse = await HttpHelper.GetStringAsync(_httpClient, queryDocumento, _logger, "Error en la consulta a SAP");
        if (string.IsNullOrEmpty(jsonResponse))
        {
            return null;
        }

        var rawJson = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonResponse);
        if (rawJson == null || !rawJson.ContainsKey("value"))
        {
            _logger.LogWarning("No se encontraron datos en la respuesta de las Facturas canceladas.");
            return null;
        }

        var documentosJson = rawJson["value"].ToString();
        var documentosResponse = JsonConvert.DeserializeObject<List<DocumentosCancelados>>(documentosJson);

        // Lista final de los documentos cancelados
        var documentoList = new List<EventoCancelacion>();

        foreach(var doc in documentosResponse)
        {
            var eventoCancelacion = new EventoCancelacion
            {
                DocEntry = doc.DocEntry,
                CDC = doc.U_EXX_FE_CDC.Trim(),
                Motivo = doc.Comments
            };

            documentoList.Add(eventoCancelacion);
        }

        return documentoList;
    }

    public async Task<List<EventoCancelacion>> GetEventoNotaCreditoCancelada()
    {
        string queryDocumento = "CreditNotes?$select=DocEntry,DocNum,U_EXX_FE_CDC,Comments,DocumentStatus,CancelStatus,U_EXX_FE_Estado,U_EXX_FE_ANULACION_ESTADO,DocDate&$filter=CancelStatus eq 'csYes' and U_EXX_FE_Estado eq 'AUT' and U_EXX_FE_ANULACION_ESTADO eq 'NEN' and DocDate ge '2026-06-01'";

        var jsonResponse = await HttpHelper.GetStringAsync(_httpClient, queryDocumento, _logger, "Error en la consulta a SAP");
        if (string.IsNullOrEmpty(jsonResponse))
        {
            return null;
        }

        var rawJson = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonResponse);
        if (rawJson == null || !rawJson.ContainsKey("value"))
        {
            _logger.LogWarning("No se encontraron datos en la respuesta de las Notas de crédito canceladas.");
            return null;
        }

        var documentosJson = rawJson["value"].ToString();
        var documentosResponse = JsonConvert.DeserializeObject<List<DocumentosCancelados>>(documentosJson);

        // Lista final de los documentos cancelados
        var documentoList = new List<EventoCancelacion>();

        foreach(var doc in documentosResponse)
        {
            var eventoCancelacion = new EventoCancelacion
            {
                DocEntry = doc.DocEntry,
                CDC = doc.U_EXX_FE_CDC.Trim(),
                Motivo = doc.Comments
            };

            documentoList.Add(eventoCancelacion);
        }

        return documentoList;
    }

    public async Task<List<EventoCancelacion>> GetEventoNotaRemisionCancelada()
    {
        string queryDocumento = "DeliveryNotes?$select=DocEntry,DocNum,U_EXX_FE_CDC,Comments,DocumentStatus,CancelStatus,U_EXX_FE_Estado,U_EXX_FE_ANULACION_ESTADO,DocDate&$filter=CancelStatus eq 'csYes' and U_EXX_FE_Estado eq 'AUT' and U_EXX_FE_ANULACION_ESTADO eq 'NEN' and DocDate ge '2026-06-01'";

        var jsonResponse = await HttpHelper.GetStringAsync(_httpClient, queryDocumento, _logger, "Error en la consulta a SAP");
        if (string.IsNullOrEmpty(jsonResponse))
        {
            return null;
        }

        var rawJson = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonResponse);
        if (rawJson == null || !rawJson.ContainsKey("value"))
        {
            _logger.LogWarning("No se encontraron datos en la respuesta de las Notas de remisión canceladas.");
            return null;
        }

        var documentosJson = rawJson["value"].ToString();
        var documentosResponse = JsonConvert.DeserializeObject<List<DocumentosCancelados>>(documentosJson);

        // Lista final de los documentos cancelados
        var documentoList = new List<EventoCancelacion>();

        foreach(var doc in documentosResponse)
        {
            var eventoCancelacion = new EventoCancelacion
            {
                DocEntry = doc.DocEntry,
                CDC = doc.U_EXX_FE_CDC.Trim(),
                Motivo = doc.Comments
            };

            documentoList.Add(eventoCancelacion);
        }

        return documentoList;
    }

}
