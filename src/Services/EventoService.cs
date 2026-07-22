using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http;
using System.Collections.Generic;
using System.Threading.Tasks;

public class EventoService
{
    private readonly SAPServiceLayer _sapServiceLayer;
    private readonly HttpClient _httpClient;
    private readonly ILogger<EventoService> _logger;

    public EventoService(SAPServiceLayer sapServiceLayer, ILogger<EventoService> logger)
    {
        _sapServiceLayer = sapServiceLayer;
        _httpClient = sapServiceLayer.GetHttpClient();
        _logger = logger;
    }

    public async Task<List<EventoInutilizacion>> GetEventoInutilizacion()
    {
        string queryDocumento = "EPY_DVAN?$select=DocEntry,U_TDOC,DocNum,U_ETTE,U_PETE,U_NROD,U_NROH,U_TIM,U_SFTE,U_FDOC,U_EXX_FE_INUTILIZA_ESTADO,U_ANUD,U_FDOC&$filter=U_FDOC gt '2026-02-01' and U_EXX_FE_INUTILIZA_ESTADO eq 'NEN'";

        var jsonResponse = await HttpHelper.GetStringAsync(_httpClient, queryDocumento, _logger, "Error en la consulta a SAP");
        if (string.IsNullOrEmpty(jsonResponse))
        {
            return null;
        }

        var rawJson = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonResponse);
        if (rawJson == null || !rawJson.ContainsKey("value"))
        {
            _logger.LogWarning("No se encontraron datos en la respuesta de EPY_DVAN.");
            return null;
        }

        // Obtener la lista de los documentos a anular y deserializar
        var documentosJson = rawJson["value"].ToString();
        var documentosResponse = JsonConvert.DeserializeObject<List<DocumentosInutilizados>>(documentosJson);

        // Lista final de los documentos anulados
        var documentoList = new List<EventoInutilizacion>();

        foreach (var doc in documentosResponse)
        {
            var evento = new EventoInutilizacion
            {
                DocEntry = doc.DocEntry,
                dNumTim = doc.U_TIM,
                dEst = doc.U_ETTE,
                dPunExp = doc.U_PETE,
                dNumIn = doc.U_NROD,
                dNumFin = doc.U_NROH,
                iTiDE = doc.U_TDOC,
                mOtEve = doc.U_ANUD
            };

            documentoList.Add(evento);
        }

        return documentoList;

    }    
}