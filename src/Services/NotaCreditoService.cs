using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

public class NotaCreditoService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NotaCreditoService> _logger;

    public NotaCreditoService(SAPServiceLayer sapServiceLayer, ILogger<NotaCreditoService> logger)
    {
        _httpClient = sapServiceLayer.GetHttpClient();
        _logger = logger;
    }

    public async Task<List<NotaCredito>> GetNotaCreditoSinCDC()
    {
        string queryDocumento = "$crossjoin(CreditNotes,BusinessPartners,Currencies) " +
            "?$expand=CreditNotes($select=DocEntry,DocType,DocRate,DocCurrency,U_EXX_FE_CDC,U_CDOC,CardCode,U_EST,U_PDE,U_TIM,U_FITE,FolioNumber,DocDate,U_EXX_FE_CODERR,U_EXX_FE_IndPresencia,PaymentGroupCode,NumberOfInstallments," +
            "U_NUMFC,U_TIMFC,U_DASO,U_EXX_FE_MotEmision,Comments)," +
            "BusinessPartners($select=CardCode,CardName,FederalTaxID,U_TIPCONT,U_CRSI,U_EXX_FE_TipoOperacion,Phone1,Cellular,EmailAddress), " +
            "Currencies($select=Code,Name,DocumentsCode) " +
            "&$filter=CreditNotes/CardCode eq BusinessPartners/CardCode and " +
            "CreditNotes/DocCurrency eq Currencies/Code and (CreditNotes/U_EXX_FE_CDC eq null or CreditNotes/U_EXX_FE_CDC eq '') and CreditNotes/U_EXX_FE_Estado eq 'NEN' and CreditNotes/Cancelled eq 'tNO' and " +
            "CreditNotes/DocDate ge '20260201' and CreditNotes/FolioNumber ne null and CreditNotes/U_DOCD eq 'S'";

        var jsonResponse = await HttpHelper.GetStringAsync(_httpClient, queryDocumento, _logger, "Error en la consulta a SAP");
        if (string.IsNullOrEmpty(jsonResponse))
        {
            _logger.LogWarning("No se encontraron datos en la respuesta de SAP.");
            return new List<NotaCredito>();
        }

        var rawJson = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonResponse);
        if (rawJson == null || !rawJson.ContainsKey("value"))
        {
            _logger.LogWarning("No se encontraron datos en la respuesta de SAP.");
            return new List<NotaCredito>();
        }

        // Obtener la lista de Notas de crédito y deserializar
        var notaCreditoJson = rawJson["value"].ToString();
        var notaCreditoResponse = JsonConvert.DeserializeObject<List<NotaCreditoResponse>>(notaCreditoJson);

        if (notaCreditoResponse == null)
        {
            _logger.LogWarning("No se pudieron deserializar las notas de crédito.");
            return new List<NotaCredito>();
        }

        // Agrupar las respuestas por DocEntry para consolidar todas las líneas de cada nota de crédito
        var notaCreditoAgrupadas = notaCreditoResponse
            .GroupBy(n => n.CreditNotes.DocEntry)
            .ToDictionary(g => g.Key, g => g.ToList());

        var cardCode = notaCreditoResponse.Select(f => f.BusinessPartners.CardCode).Distinct().ToList();
        var direcciones = await GetDireccionesSocioNegocio(cardCode);
        var paises = direcciones.Select(d => d.Country).Where(c => !string.IsNullOrEmpty(c)).Distinct().ToList();

        // Crear diccionarios para nombres y códigos de países
        var nombresCodigosPaises = new Dictionary<string, (string Nombre, string CodigoReporte)>();

        foreach (var pais in paises)
        {
            var infoCompleta = await GetInformacionPais(pais);
            nombresCodigosPaises[pais] = infoCompleta;
        }

        // Lista final de notas de crédito
        var notaCreditoList = new List<NotaCredito>();

        // Procesar cada nota de crédito agrupada con sus líneas
        foreach (var notaCreditoGroup in notaCreditoAgrupadas)
        {
            var docEntry = notaCreditoGroup.Key;
            // Tomar la primera entrada para obtener la información general de la nota de crédito
            var primeraEntrada = notaCreditoGroup.Value.First();

            if (primeraEntrada.CreditNotes == null || primeraEntrada.BusinessPartners == null || primeraEntrada.Currencies == null)
            {
                continue;
            }

            // Encontrar la dirección para este socio de negocio
            var direccion = direcciones.FirstOrDefault(d => d.CardCode == primeraEntrada.BusinessPartners.CardCode);

            // Obtener la información del país
            string descripcionPais = "";
            string codigoReportePais = "";
            string street = "";
            int? streetNo = 0;

            if (direccion != null && !string.IsNullOrEmpty(direccion.Country) && nombresCodigosPaises.ContainsKey(direccion.Country))
            {
                var infoPais = nombresCodigosPaises[direccion.Country];
                descripcionPais = infoPais.Nombre;
                codigoReportePais = infoPais.CodigoReporte;
                street = direccion.Street;
                streetNo = direccion.StreetNo;
            }

            // Crear la nota de crédito con los datos generales
            var notaCredito = new NotaCredito
            {
                DocEntry = primeraEntrada.CreditNotes.DocEntry,
                DocType = primeraEntrada.CreditNotes.DocType,
                U_EXX_FE_CDC = primeraEntrada.CreditNotes.U_EXX_FE_CDC ?? "",
                U_CDOC = primeraEntrada.CreditNotes.U_CDOC?.PadLeft(2, '0'),
                CardCode = primeraEntrada.CreditNotes.CardCode ?? "",
                U_EST = primeraEntrada.CreditNotes.U_EST ?? "",
                U_PDE = primeraEntrada.CreditNotes.U_PDE ?? "",
                FolioNum = primeraEntrada.CreditNotes.FolioNumber ?? "",
                DocDate = primeraEntrada.CreditNotes.DocDate,
                DocTime = await ObtenerDocTimePorDocEntry(docEntry),
                U_TIM = primeraEntrada.CreditNotes.U_TIM,
                U_FITE = primeraEntrada.CreditNotes.U_FITE,
                dTiCam = primeraEntrada.CreditNotes.DocRate,
                iMotEmi = primeraEntrada.CreditNotes.U_EXX_FE_MotEmision,
                iTipDocAso = primeraEntrada.CreditNotes.U_DASO,
                U_NUMFC = primeraEntrada.CreditNotes.U_NUMFC,
                timbradoSAP = primeraEntrada.CreditNotes.U_TIMFC,
                Comments = primeraEntrada.CreditNotes.Comments,

                BusinessPartner = new BusinessPartner
                {
                    CardCode = primeraEntrada.BusinessPartners.CardCode ?? "",
                    dNomRec = primeraEntrada.BusinessPartners.CardName ?? "",
                    FederalTaxID = primeraEntrada.BusinessPartners.FederalTaxID ?? "",
                    iTiContRec = primeraEntrada.BusinessPartners.U_TIPCONT ?? 0,
                    iTiOpe = primeraEntrada.BusinessPartners.U_EXX_FE_TipoOperacion,
                    iNatRec = primeraEntrada.BusinessPartners.U_CRSI ?? "",
                    cPaisRec = codigoReportePais ?? "",
                    dDesPaisRe = descripcionPais,
                    dDirRec = street,
                    dNumCasRec = streetNo,
                    dTelRec = primeraEntrada.BusinessPartners.Phone1,
                    dCelRec = primeraEntrada.BusinessPartners.Cellular,
                    dEmailRec = primeraEntrada.BusinessPartners.EmailAddress
                },
                Currencies = new Currencies
                {
                    cMoneOpe = primeraEntrada.Currencies.DocumentsCode ?? "",
                    dDesMoneOpe = primeraEntrada.Currencies.Name ?? ""
                },
                Items = new List<Item>()
            };

            // Obtener las líneas para este DocEntry específico
            await ObtenerLineasNotaCredito(notaCredito, docEntry);
            notaCreditoList.Add(notaCredito);
        }

        return notaCreditoList;
    }

    private async Task<int> ObtenerDocTimePorDocEntry(int docEntry)
    {
        string query = $"CreditNotes?$select=DocEntry,DocTime&$filter=DocEntry eq {docEntry}";
        var jsonResponse = await HttpHelper.GetStringAsync(_httpClient, query, _logger, $"Error al obtener DocTime para DocEntry {docEntry}");

        if (string.IsNullOrEmpty(jsonResponse)) return 0;

        var rawJson = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonResponse);
        if (rawJson == null || !rawJson.ContainsKey("value")) return 0;

        var valueJson = rawJson["value"].ToString();
        var notaCreditoDocs = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(valueJson);

        if (notaCreditoDocs == null || notaCreditoDocs.Count == 0) return 0;

        if (notaCreditoDocs[0].ContainsKey("DocTime"))
        {
            var docTimeStr = notaCreditoDocs[0]["DocTime"]?.ToString();

            if (TimeSpan.TryParse(docTimeStr, out var ts))
            {
                return ts.Hours * 10000 + ts.Minutes * 100 + ts.Seconds;
            }
        }

        return 0;
    }

    private async Task ObtenerLineasNotaCredito(NotaCredito notaCredito, int docEntry)
    {
        string queryLineas = $"CreditNotes({docEntry})/DocumentLines";
        var responseLineas = await _httpClient.GetAsync(queryLineas);

        if (responseLineas.IsSuccessStatusCode)
        {
            try
            {
                var jsonResponseLineas = await responseLineas.Content.ReadAsStringAsync();
                var responseObj = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonResponseLineas);

                if (responseObj != null && responseObj.ContainsKey("DocumentLines"))
                {
                    var lineasResponse = JsonConvert.DeserializeObject<List<DocumentLineData>>(responseObj["DocumentLines"].ToString());

                    if (lineasResponse != null)
                    {
                        foreach (var linea in lineasResponse)
                        {
                            notaCredito.Items.Add(new Item
                            {
                                dCodInt = notaCredito.DocType == "S" ? "1" : linea.ItemCode,
                                //    dDesProSer = linea.ItemDescription,
                                dDesProSer = notaCredito.DocType == "S" ? notaCredito.Comments : linea.ItemDetails,
                                dCantProSer = notaCredito.DocType == "S" ? 1 : linea.Quantity,
                                dPUniProSer = linea.PriceAfterVAT,
                                dTiCamIt = linea.Rate,
                                taxCode = linea.TaxCode,
                                dTasaIVA = linea.TaxPercentagePerRow
                            });
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"No se pudieron deserializar las líneas de la Nota de crédito {docEntry}");
                    }
                }
                else
                {
                    _logger.LogWarning($"No se encontraron líneas para la Nota de crédito {docEntry}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error al procesar las líneas para la Nota de crédito {docEntry}: {ex.Message}");
                if (ex.InnerException != null)
                {
                    _logger.LogError($"Error interno: {ex.InnerException.Message}");
                }
            }
        }
        else
        {
            var errorContent = await responseLineas.Content.ReadAsStringAsync();
            _logger.LogError($"Error al obtener líneas para la Nota de crédito {docEntry}: {responseLineas.StatusCode}");
            _logger.LogError($"Detalles: {errorContent}");
        }
    }

    public async Task<List<BusinessPartnerData.BPAddressInfo>> GetDireccionesSocioNegocio(List<string> cardCodes)
    {
        var direcciones = new List<BusinessPartnerData.BPAddressInfo>();

        foreach (var cardCode in cardCodes)
        {
            try
            {
                // Consultamos a la colección de direcciones
                string queryDirecciones = $"BusinessPartners('{cardCode}')/BPAddresses";

                // Usando el método auxiliar para obtener la respuesta
                var jsonResponse = await HttpHelper.GetStringAsync(_httpClient, queryDirecciones, _logger, $"Error al obtener direcciones para {cardCode}");

                if (string.IsNullOrEmpty(jsonResponse))
                {
                    continue;
                }

                // Deserializar como un objeto JSON dinámico para luego acceder al array BPAddresses
                var responseObj = JsonConvert.DeserializeObject<BPAddressesWrapper>(jsonResponse);

                if (responseObj == null || responseObj.BPAddresses == null || !responseObj.BPAddresses.Any())
                {
                    _logger.LogWarning($"No se encontraron direcciones para {cardCode}.");
                    continue;
                }

                // Tomar la primera dirección
                var primeraDireccion = responseObj.BPAddresses.FirstOrDefault();

                if (primeraDireccion != null)
                {
                    direcciones.Add(new BusinessPartnerData.BPAddressInfo
                    {
                        CardCode = cardCode,
                        Country = primeraDireccion.Country ?? "",
                        Street = primeraDireccion.Street ?? "",
                        StreetNo = primeraDireccion.StreetNo
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error al procesar direcciones para {cardCode}: {ex.Message}");

                if (ex.InnerException != null)
                {
                    _logger.LogError($"Error interno: {ex.InnerException.Message}");
                }
            }
        }

        return direcciones;
    }

    public async Task<(string Name, string CodeForReports)> GetInformacionPais(string codigoPais)
    {
        try
        {
            string query = $"Countries?$select=Code,Name,CodeForReports&$filter=Code eq '{codigoPais}'";
            var jsonResponse = await HttpHelper.GetStringAsync(_httpClient, query, _logger, $"Error al obtener información del país {codigoPais}");

            if (string.IsNullOrEmpty(jsonResponse))
            { 
                return ("", "");
            }

            var responseObj = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonResponse);

            if (responseObj == null || !responseObj.ContainsKey("value"))
            {
                _logger.LogWarning($"Formato de respuesta inesperado para el país {codigoPais}");
                return ("", "");
            }

            // Acceder al array de resultados
            var valueArray = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(responseObj["value"].ToString());

            if (valueArray == null || valueArray.Count == 0)
            {
                _logger.LogWarning($"No se encontró información para el país {codigoPais}");
                return ("", "");
            }

            // Obtener la información del país
            var paisInfo = valueArray[0];
            string name = paisInfo.ContainsKey("Name") ? paisInfo["Name"].ToString() : "";
            string codeForReports = paisInfo.ContainsKey("CodeForReports") ? paisInfo["CodeForReports"].ToString() : "";

            return (name, codeForReports);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error al obtener información del país {codigoPais}: {ex.Message}");
            return ("", "");
        }
    }

    public async Task<(string? dCdCDERef, int? dNTimDI, DateTime? dFecEmiDI)> ObtenerCDCFactura(string? dEstDocAso, string? dPExpDocAso, string? dNumDocAso, string? rucCompleto, int? timbradoSAP)
    {
        try
        {
            string query = $"Invoices?$select=DocDate,U_TIM,U_EXX_FE_CDC&$filter=FederalTaxID eq '{rucCompleto}' and U_EST eq '{dEstDocAso}' and U_PDE eq '{dPExpDocAso}' and FolioNumber eq {dNumDocAso} and U_TIM eq '{timbradoSAP}'";
            var jsonResponse = await HttpHelper.GetStringAsync(_httpClient, query, _logger, "Error al obtener datos de factura referenciada");

            if (string.IsNullOrWhiteSpace(jsonResponse))
                return (null, null, null);

            var root = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonResponse);

            if (root == null || !root.ContainsKey("value"))
                return (null, null, null);

            var valueArray = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(root["value"].ToString());

            if (valueArray == null || valueArray.Count == 0)
                return (null, null, null);

            var factura = valueArray[0];

            string dCdCDERef = factura.ContainsKey("U_EXX_FE_CDC") ? factura["U_EXX_FE_CDC"]?.ToString() : null;
            int? dNTimDI = factura.ContainsKey("U_TIM") ? int.Parse(factura["U_TIM"].ToString()) : null;

            DateTime? dFecEmiDI = null;
            if (factura.ContainsKey("DocDate"))
            {
                DateTime parsed;
                if (DateTime.TryParse(factura["DocDate"].ToString(), out parsed))
                    dFecEmiDI = parsed;
            }

            return (dCdCDERef, dNTimDI, dFecEmiDI);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error al obtener datos de factura referenciada: {ex.Message}");
            return (null, null, null);
        }
    }

    public async Task<List<NotaCredito>> GetNotaCreditoSinAutorizar()
    {
        string queryDocumento = "$crossjoin(CreditNotes,BusinessPartners,Currencies) " +
            "?$expand=CreditNotes($select=DocEntry,DocType,DocRate,DocCurrency,U_EXX_FE_CDC,U_CDOC,CardCode,U_EST,U_PDE,U_TIM,U_FITE,FolioNumber,DocDate,U_EXX_FE_Estado,U_EXX_FE_CODERR,U_EXX_FE_IndPresencia,PaymentGroupCode,NumberOfInstallments,U_NUMFC,U_TIMFC,U_DASO,U_EXX_FE_MotEmision,Comments)," +
            "BusinessPartners($select=CardCode,CardName,FederalTaxID,U_TIPCONT,U_CRSI,U_EXX_FE_TipoOperacion), " +
            "Currencies($select=Code,Name,DocumentsCode) " +
            "&$filter=CreditNotes/CardCode eq BusinessPartners/CardCode and " +
            "CreditNotes/DocCurrency eq Currencies/Code and " +
            "CreditNotes/FolioNumber ne null and " +
            "CreditNotes/DocDate ge '20260201' and " +
            "CreditNotes/U_EXX_FE_Estado ne 'AUT' and CreditNotes/Cancelled eq 'tNO' and " +
            "CreditNotes/U_EXX_FE_CDC ne null and CreditNotes/U_EXX_FE_CDC ne '' and CreditNotes/U_DOCD eq 'S' ";

        var jsonResponse = await HttpHelper.GetStringAsync(_httpClient, queryDocumento, _logger, "Error en la consulta a SAP");
        if (string.IsNullOrEmpty(jsonResponse))
        {
            _logger.LogWarning("No se encontraron datos en la respuesta de SAP.");
            return new List<NotaCredito>();
        }

        var rawJson = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonResponse);
        if (rawJson == null || !rawJson.ContainsKey("value"))
        {
            _logger.LogWarning("No se encontraron datos en la respuesta de SAP.");
            return new List<NotaCredito>();
        }

        // Obtener la lista de Notas de crédito y deserializar
        var notaCreditoJson = rawJson["value"].ToString();
        var notaCreditoResponse = JsonConvert.DeserializeObject<List<NotaCreditoResponse>>(notaCreditoJson);

        if (notaCreditoResponse == null)
        {
            _logger.LogWarning("No se pudieron deserializar las notas de crédito.");
            return new List<NotaCredito>();
        }

        // Agrupar las respuestas por DocEntry para consolidar todas las líneas de cada nota de crédito
        var notaCreditoAgrupadas = notaCreditoResponse
            .GroupBy(n => n.CreditNotes.DocEntry)
            .ToDictionary(g => g.Key, g => g.ToList());

        var cardCode = notaCreditoResponse.Select(f => f.BusinessPartners.CardCode).Distinct().ToList();
        var direcciones = await GetDireccionesSocioNegocio(cardCode);
        var paises = direcciones.Select(d => d.Country).Where(c => !string.IsNullOrEmpty(c)).Distinct().ToList();

        // Crear diccionarios para nombres y códigos de países
        var nombresCodigosPaises = new Dictionary<string, (string Nombre, string CodigoReporte)>();

        foreach (var pais in paises)
        {
            var infoCompleta = await GetInformacionPais(pais);
            nombresCodigosPaises[pais] = infoCompleta;
        }

        // Lista final de notas de crédito
        var notaCreditoList = new List<NotaCredito>();

        // Procesar cada nota de crédito agrupada con sus líneas
        foreach (var notaCreditoGroup in notaCreditoAgrupadas)
        {
            var docEntry = notaCreditoGroup.Key;
            // Tomar la primera entrada para obtener la información general de la nota de crédito
            var primeraEntrada = notaCreditoGroup.Value.First();

            if (primeraEntrada.CreditNotes == null || primeraEntrada.BusinessPartners == null || primeraEntrada.Currencies == null)
            {
                continue;
            }

            // Encontrar la dirección para este socio de negocio
            var direccion = direcciones.FirstOrDefault(d => d.CardCode == primeraEntrada.BusinessPartners.CardCode);

            // Obtener la información del país
            string descripcionPais = "";
            string codigoReportePais = "";
            string street = "";
            int? streetNo = 0;

            if (direccion != null && !string.IsNullOrEmpty(direccion.Country) && nombresCodigosPaises.ContainsKey(direccion.Country))
            {
                var infoPais = nombresCodigosPaises[direccion.Country];
                descripcionPais = infoPais.Nombre;
                codigoReportePais = infoPais.CodigoReporte;
                street = direccion.Street;
                streetNo = direccion.StreetNo;
            }

            // Crear la nota de crédito con los datos generales
            var notaCredito = new NotaCredito
            {
                DocEntry = primeraEntrada.CreditNotes.DocEntry,
                DocType = primeraEntrada.CreditNotes.DocType,
                U_EXX_FE_CDC = primeraEntrada.CreditNotes.U_EXX_FE_CDC ?? "",
                U_EXX_FE_Estado = primeraEntrada.CreditNotes.U_EXX_FE_Estado,
                U_EXX_FE_CODERR = primeraEntrada.CreditNotes.U_EXX_FE_CODERR,
                U_CDOC = primeraEntrada.CreditNotes.U_CDOC?.PadLeft(2, '0'),
                CardCode = primeraEntrada.CreditNotes.CardCode ?? "",
                U_EST = primeraEntrada.CreditNotes.U_EST ?? "",
                U_PDE = primeraEntrada.CreditNotes.U_PDE ?? "",
                FolioNum = primeraEntrada.CreditNotes.FolioNumber ?? "",
                DocDate = primeraEntrada.CreditNotes.DocDate,
                DocTime = await ObtenerDocTimePorDocEntry(docEntry),
                U_TIM = primeraEntrada.CreditNotes.U_TIM,
                U_FITE = primeraEntrada.CreditNotes.U_FITE,
                dTiCam = primeraEntrada.CreditNotes.DocRate,
                iMotEmi = primeraEntrada.CreditNotes.U_EXX_FE_MotEmision,
                iTipDocAso = primeraEntrada.CreditNotes.U_DASO,
                U_NUMFC = primeraEntrada.CreditNotes.U_NUMFC,
                timbradoSAP = primeraEntrada.CreditNotes.U_TIMFC,
                Comments = primeraEntrada.CreditNotes.Comments,

                BusinessPartner = new BusinessPartner
                {
                    CardCode = primeraEntrada.BusinessPartners.CardCode ?? "",
                    dNomRec = primeraEntrada.BusinessPartners.CardName ?? "",
                    FederalTaxID = primeraEntrada.BusinessPartners.FederalTaxID ?? "",
                    iTiContRec = primeraEntrada.BusinessPartners.U_TIPCONT ?? 0,
                    iTiOpe = primeraEntrada.BusinessPartners.U_EXX_FE_TipoOperacion,
                    iNatRec = primeraEntrada.BusinessPartners.U_CRSI ?? "",
                    cPaisRec = codigoReportePais ?? "",
                    dDesPaisRe = descripcionPais,
                    dDirRec = street,
                    dNumCasRec = streetNo,
                    dTelRec = primeraEntrada.BusinessPartners.Phone1,
                    dCelRec = primeraEntrada.BusinessPartners.Cellular,
                    dEmailRec = primeraEntrada.BusinessPartners.EmailAddress
                },
                Currencies = new Currencies
                {
                    cMoneOpe = primeraEntrada.Currencies.DocumentsCode ?? "",
                    dDesMoneOpe = primeraEntrada.Currencies.Name ?? ""
                },
                Items = new List<Item>()
            };

            // Obtener las líneas para este DocEntry específico
            await ObtenerLineasNotaCredito(notaCredito, docEntry);
            notaCreditoList.Add(notaCredito);
        }

        return notaCreditoList;
    }

}