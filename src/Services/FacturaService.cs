using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

public class FacturaService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FacturaService> _logger;

    public FacturaService(SAPServiceLayer sapServiceLayer, ILogger<FacturaService> logger)
    {
        _httpClient = sapServiceLayer.GetHttpClient();
        _logger = logger;
    }

    public async Task<List<Factura>> GetFacturasSinCDC()
    {
        string queryDocumento = "$crossjoin(Invoices,BusinessPartners,Currencies)" +
            "?$expand=Invoices($select=DocEntry,DocRate,DocType,DocCurrency,U_FE_CDC,U_CENT_TIPO_DOC,CardCode,U_CENT_EST,U_CENT_PE,U_CENT_TIMB,U_FITE,FolioNumber,DocDate,U_iTipTra,U_FE_IndPresencia,PaymentGroupCode," +
            "NumberOfInstallments,Comments)," +
            "BusinessPartners($select=CardCode,CardName,FederalTaxID,U_CENT_TIP_SN,U_CENT_SITUACION,U_CENT_TIP_OPE,U_CENT_IDENT_TIPO,Phone1,Cellular,EmailAddress)," +
            "Currencies($select=Code,Name,DocumentsCode)" +
            "&$filter=Invoices/CardCode eq BusinessPartners/CardCode and " +
            "Invoices/DocCurrency eq Currencies/Code and " +
            "(Invoices/U_FE_CDC eq null or Invoices/U_FE_CDC eq '') and Invoices/U_CENT_DECLARABLE eq 'S' and Invoices/U_FE_Estado eq 'NEN' and Invoices/Cancelled eq 'tNO' and " +
            "Invoices/DocDate ge '20260301' and Invoices/FolioNumber ne null";
        //    "Invoices/DocEntry eq 3480";

        var jsonResponse = await HttpHelper.GetStringAsync(_httpClient, queryDocumento, _logger, "Error en la consulta a SAP");
        if (string.IsNullOrEmpty(jsonResponse))
        {
            _logger.LogWarning("No se encontraron datos en la respuesta de SAP.");
            return new List<Factura>();
        }

        var rawJson = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonResponse);
        if (rawJson == null || !rawJson.ContainsKey("value"))
        {
            _logger.LogWarning("No se encontraron datos en la respuesta de SAP.");
            return new List<Factura>();
        }

        // Obtener la lista de facturas y deserializar
        var facturasJson = rawJson["value"].ToString();
        var facturasResponse = JsonConvert.DeserializeObject<List<FacturaResponse>>(facturasJson);

        if (facturasResponse == null)
        {
            _logger.LogWarning("No se pudieron deserializar las facturas.");
            return new List<Factura>();
        }

        // Agrupar las respuestas por DocEntry para consolidar todas las líneas de cada factura
        var facturasAgrupadas = facturasResponse
            .GroupBy(f => f.Invoices.DocEntry)
            .ToDictionary(g => g.Key, g => g.ToList());

        var cardCode = facturasResponse.Select(f => f.BusinessPartners.CardCode).Distinct().ToList();
        var direcciones = await GetDireccionesSocioNegocio(cardCode);
        var paises = direcciones.Select(d => d.Country).Where(c => !string.IsNullOrEmpty(c)).Distinct().ToList();

        // Crear diccionarios para nombres y códigos de países
        var nombresCodigosPaises = new Dictionary<string, (string Nombre, string CodigoReporte)>();

        foreach (var pais in paises)
        {
            var infoCompleta = await GetInformacionPais(pais);
            nombresCodigosPaises[pais] = infoCompleta;
        }

        // Lista final de facturas
        var facturasList = new List<Factura>();

        // Procesar cada factura agrupada con sus líneas
        foreach (var facturaGroup in facturasAgrupadas)
        {
            var docEntry = facturaGroup.Key;
            // Tomar la primera entrada para obtener la información general de la factura
            var primeraEntrada = facturaGroup.Value.First();

            if (primeraEntrada.Invoices == null || primeraEntrada.BusinessPartners == null || primeraEntrada.Currencies == null)
            {
                continue;
            }

            // Encontrar la dirección para este socio de negocio
            var direccion = direcciones.FirstOrDefault(d => d.CardCode == primeraEntrada.BusinessPartners.CardCode);

            // Obtener la información del Socio de negocios
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

            // Crear la factura con los datos generales
            var factura = new Factura
            {
                DocEntry = primeraEntrada.Invoices.DocEntry,
                DocType = primeraEntrada.Invoices.DocType,
                U_FE_CDC = primeraEntrada.Invoices.U_FE_CDC ?? "",
                U_CENT_TIPO_DOC = primeraEntrada.Invoices.U_CENT_TIPO_DOC?.PadLeft(2, '0'),
                CardCode = primeraEntrada.Invoices.CardCode ?? "",
                U_CENT_EST = primeraEntrada.Invoices.U_CENT_EST ?? "",
                U_CENT_PE = primeraEntrada.Invoices.U_CENT_PE ?? "",
                FolioNum = primeraEntrada.Invoices.FolioNumber ?? "",
                DocDate = primeraEntrada.Invoices.DocDate,
                DocTime = await ObtenerDocTimePorDocEntry(docEntry),
                U_CENT_TIMB = primeraEntrada.Invoices.U_CENT_TIMB,
                U_FITE = primeraEntrada.Invoices.U_FITE,
                iTipTra = primeraEntrada.Invoices.U_iTipTra,
                iIndPres = primeraEntrada.Invoices.U_FE_IndPresencia,
                iCondOpe = primeraEntrada.Invoices.PaymentGroupCode,
                iCondCred = primeraEntrada.Invoices.NumberOfInstallments,
                dTiCam = primeraEntrada.Invoices.DocRate,
                Comments = primeraEntrada.Invoices.Comments,
                BusinessPartner = new BusinessPartner
                {
                    CardCode = primeraEntrada.BusinessPartners.CardCode ?? "",
                    dNomRec = primeraEntrada.BusinessPartners.CardName ?? "",
                    FederalTaxID = primeraEntrada.BusinessPartners.FederalTaxID ?? "",
                    iTiContRec = primeraEntrada.BusinessPartners.U_CENT_TIP_SN ?? 0,
                    iTiOpe = primeraEntrada.BusinessPartners.U_CENT_TIP_OPE,
                    iNatRec = primeraEntrada.BusinessPartners.U_CENT_SITUACION ?? "",
                    iTipIDRec = primeraEntrada.BusinessPartners.U_CENT_IDENT_TIPO,
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
            await ObtenerLineasFactura(factura, docEntry);
            facturasList.Add(factura);
        }

        // Inicializar operación de crédito y obtener cuotas para facturas a crédito
        foreach (var factura in facturasList)
        {
            // Normalizar la condición de operación y condición de crédito
            int condicionOperacion = factura.iCondOpe == -1 ? 1 : 2;
            int condicionCredito = factura.iCondCred == 1 ? 1 : 2;

            // Solo inicializar la operación de crédito si la condición de operación es crédito (2)
            if (condicionOperacion == 2)
            {
                // Obtener el plazo de crédito según la condición
                string plazoCredito = "";
                if (condicionCredito == 1)
                {
                    plazoCredito = await ObtenerPlazoCredito(factura.DocEntry);
                }

                int? cantidadCuotas = condicionCredito == 2 ? factura.iCondCred : null;

                // Inicializar la operación de crédito
                factura.OperacionCredito = new GPagCred(condicionCredito, plazoCredito, cantidadCuotas);

                // Si es por cuotas, obtener las cuotas
                if (condicionCredito == 2)
                {
                    try
                    {
                        var cuotasResponse = await GetCuotasFactura(factura.DocEntry);

                        if (cuotasResponse != null && cuotasResponse.Any())
                        {
                            foreach (var cuota in cuotasResponse)
                            {
                                if (DateTime.TryParse(cuota.U_FECHAV, out DateTime fechaVencimiento))
                                {
                                    // Determinar el monto de la cuota
                                    decimal montoCuota = cuota.TotalFC > 0 ? cuota.TotalFC : cuota.Total;

                                    var nuevaCuota = new GCuotas(
                                        factura.Currencies.cMoneOpe,         // Moneda de la cuota
                                        factura.Currencies.dDesMoneOpe,      // Descripción de la moneda
                                        montoCuota,                          // Monto de la cuota
                                        fechaVencimiento                     // Fecha de vencimiento
                                    );

                                    factura.OperacionCredito.Cuotas.Add(nuevaCuota);
                                }
                                else
                                {
                                    _logger.LogWarning($"Advertencia: Formato de fecha inválido en cuota: {cuota.U_FECHAV}");
                                }
                            }
                        }
                        else
                        {
                            _logger.LogWarning($"No se encontraron cuotas para la factura {factura.DocEntry}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error al obtener cuotas para DocEntry {factura.DocEntry}: {ex.Message}");
                        if (ex.InnerException != null)
                        {
                            _logger.LogError($"Error interno: {ex.InnerException.Message}");
                        }
                    }
                }
            }

            factura.PagoContado = await GetPagoContado(factura.DocEntry);

        }
        return facturasList;
    }

    private async Task<int> ObtenerDocTimePorDocEntry(int docEntry)
    {
        string query = $"Invoices?$select=DocEntry,DocTime&$filter=DocEntry eq {docEntry}";
        var jsonResponse = await HttpHelper.GetStringAsync(_httpClient, query, _logger, $"Error al obtener DocTime para DocEntry {docEntry}");

        if (string.IsNullOrEmpty(jsonResponse)) return 0;

        var rawJson = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonResponse);
        if (rawJson == null || !rawJson.ContainsKey("value")) return 0;

        var valueJson = rawJson["value"].ToString();
        var facturaDocs = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(valueJson);

        if (facturaDocs == null || facturaDocs.Count == 0) return 0;

        if (facturaDocs[0].ContainsKey("DocTime"))
        {
            var docTimeStr = facturaDocs[0]["DocTime"]?.ToString();

            if (TimeSpan.TryParse(docTimeStr, out var ts))
            {
                return ts.Hours * 10000 + ts.Minutes * 100 + ts.Seconds;
            }
        }

        return 0;
    }

    private async Task ObtenerLineasFactura(Factura factura, int docEntry)
    {
        string queryLineas = $"Invoices({docEntry})/DocumentLines";
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
                            factura.Items.Add(new Item
                            {
                                dCodInt = factura.DocType == "S" ? "1" : linea.ItemCode,
                                //    dDesProSer = linea.ItemDescription,
                                dDesProSer = factura.DocType == "S" ? factura.Comments : linea.ItemDetails,
                                dCantProSer = factura.DocType == "S" ? 1 : linea.Quantity,
                                dPUniProSer = linea.PriceAfterVAT,
                                dTiCamIt = linea.Rate,
                                taxCode = linea.TaxCode,
                                dTasaIVA = linea.TaxPercentagePerRow
                            });
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"No se pudieron deserializar las líneas de la factura {docEntry}");
                    }
                }
                else
                {
                    _logger.LogWarning($"No se encontraron líneas para la factura {docEntry}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error al procesar las líneas para la factura {docEntry}: {ex.Message}");
                if (ex.InnerException != null)
                {
                    _logger.LogError($"Error interno: {ex.InnerException.Message}");
                }
            }
        }
        else
        {
            var errorContent = await responseLineas.Content.ReadAsStringAsync();
            _logger.LogError($"Error al obtener líneas para la factura {docEntry}: {responseLineas.StatusCode}");
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
            string? name = paisInfo.ContainsKey("Name") ? paisInfo["Name"].ToString() : "";
            string? codeForReports = paisInfo.ContainsKey("CodeForReports") ? paisInfo["CodeForReports"].ToString() : "";

            return (name, codeForReports);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error al obtener información del país {codigoPais}: {ex.Message}");
            return ("", "");
        }
    }

    private async Task<List<CuotaResponse>> GetCuotasFactura(int docEntry)
    {
        try
        {
            string query = $"Invoices({docEntry})";

            var jsonResponse = await HttpHelper.GetStringAsync(_httpClient, query, _logger, $"Error al obtener la factura {docEntry}");

            if (string.IsNullOrEmpty(jsonResponse))
            {
                return new List<CuotaResponse>();
            }

            if (jsonResponse.Contains("\"DocumentInstallments\""))
            {
                // Si la respuesta contiene DocumentInstallments, intentar extraerlas
                var facturaObj = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonResponse);

                if (facturaObj != null && facturaObj.ContainsKey("DocumentInstallments"))
                {
                    var cuotasJson = facturaObj["DocumentInstallments"].ToString();
                    var cuotasList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(cuotasJson);

                    if (cuotasList != null && cuotasList.Any())
                    {
                        return cuotasList.Select(cuota => new CuotaResponse
                        {
                            InstallmentId = cuota.ContainsKey("InstallmentId") ? Convert.ToInt32(cuota["InstallmentId"]) : 0,
                            Total = cuota.ContainsKey("Total") ? Convert.ToDecimal(cuota["Total"]) : 0,
                            TotalFC = cuota.ContainsKey("TotalFC") ? Convert.ToDecimal(cuota["TotalFC"]) : 0,
                            U_FECHAV = cuota.ContainsKey("U_FECHAV") ? cuota["U_FECHAV"]?.ToString() : (cuota.ContainsKey("DueDate") ? cuota["DueDate"]?.ToString() : null)
                        }).ToList();
                    }
                }
            }

            _logger.LogWarning($"No se encontraron las cuotas para la factura {docEntry}");
            return new List<CuotaResponse>();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error al obtener cuotas: {ex.Message}");
            if (ex.InnerException != null)
            {
                _logger.LogError($"Error interno: {ex.InnerException.Message}");
            }
            return new List<CuotaResponse>();
        }
    }

    private async Task<string> ObtenerPlazoCredito(int docEntry)
    {
        try
        {
            string query = $"Invoices({docEntry})?$select=PaymentGroupCode";
            var jsonResponse = await HttpHelper.GetStringAsync(_httpClient, query, _logger, $"Error al obtener el código de pago para {docEntry}");

            if (string.IsNullOrEmpty(jsonResponse))
            {
                return null;
            }

            var bp = JsonConvert.DeserializeObject<dynamic>(jsonResponse);

            // Obtener el código de términos de pago
            int payTermCode = bp.PaymentGroupCode;

            // Consultar la descripción de los términos de pago
            query = $"PaymentTermsTypes({payTermCode})";
            jsonResponse = await HttpHelper.GetStringAsync(_httpClient, query, _logger, $"Error al obtener términos de pago para código {payTermCode}");

            if (!string.IsNullOrEmpty(jsonResponse))
            {
                var payTerm = JsonConvert.DeserializeObject<dynamic>(jsonResponse);
                string months = payTerm.PaymentTermsGroupName ?? "0";
                return months;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error al obtener plazo de crédito: {ex.Message}");
            return null;
        }
    }

    public async Task<GPaConEIni> GetPagoContado(int docEntryFactura)
    {
        try
        {
            string query = $"IncomingPayments?$select=DocEntry,DocCurrency,DocRate,TransferSum,CashSum,PaymentInvoices&$orderby=DocEntry desc&$top=100";
            //string query = $"IncomingPayments?$select=DocEntry,DocCurrency,DocRate,TransferSum,CashSum,PaymentInvoices,DocDate&$filter=DocEntry eq 10443";
            var jsonResponse = await HttpHelper.GetStringAsync(_httpClient, query, _logger, $"Error al consultar pagos recibidos");

            if (string.IsNullOrEmpty(jsonResponse))
            {
                _logger.LogWarning("No se recibió respuesta de IncomingPayments.");
                return null;
            }

            var result = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonResponse);
            if (result == null || !result.ContainsKey("value"))
            {
                _logger.LogWarning("Formato de respuesta inválido para IncomingPayments.");
                return null;
            }

            var pagos = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(result["value"].ToString());
            foreach (var pago in pagos)
            {
                if (!pago.ContainsKey("PaymentInvoices") || pago["PaymentInvoices"] == null)
                    continue;

                var invoicesJson = pago["PaymentInvoices"].ToString();
                var invoices = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(invoicesJson);

                if (invoices.Any(i => Convert.ToInt32(i["DocEntry"]) == docEntryFactura))
                {
                    string? docCurrency = pago.ContainsKey("DocCurrency") ? pago["DocCurrency"]?.ToString() : "PYG";
                    
                    //En caso que el código de la monenda sea GS, colocamos el código de la moneda según ISO 4217
                    if (!string.IsNullOrEmpty(docCurrency))
                    {
                        if (docCurrency.Equals("GS", StringComparison.OrdinalIgnoreCase))
                            docCurrency = "PYG";
                        else
                            docCurrency = "USD";
                    }
                    else
                    {
                        docCurrency = "PYG";
                    }
                    
                    decimal docRate = pago.ContainsKey("DocRate") ? Convert.ToDecimal(pago["DocRate"]) : 1;

                    decimal transferSum = 0;
                    decimal cashSum = 0;

                    if (docCurrency == "PYG")
                    {
                        transferSum = pago.ContainsKey("TransferSum") ? Convert.ToDecimal(pago["TransferSum"]) : 0;
                        cashSum = pago.ContainsKey("CashSum") ? Convert.ToDecimal(pago["CashSum"]) : 0;
                    }
                    else
                    {
                        // Buscar la línea de PaymentInvoices que corresponde a la factura
                        var linea = invoices.FirstOrDefault(i => Convert.ToInt32(i["DocEntry"]) == docEntryFactura);

                        // Tomar AppliedFC (monto en moneda extranjera) desde esa línea
                        decimal appliedFC = 0m;
                        if (linea != null && linea.ContainsKey("AppliedFC") && linea["AppliedFC"] != null)
                            appliedFC = Convert.ToDecimal(linea["AppliedFC"]);

                        if (appliedFC == 0m && linea != null && linea.ContainsKey("SumApplied") && linea["SumApplied"] != null)
                        {
                            var sumAppliedLocal = Convert.ToDecimal(linea["SumApplied"]);
                            appliedFC = (docRate > 0) ? sumAppliedLocal / docRate : 0m;
                        }

                        transferSum = appliedFC > 0 && docRate > 0 ? appliedFC * docRate : 0m;
                    }                 

                    decimal montoBase = transferSum > 0 ? transferSum : cashSum;

                    if (montoBase > 0)
                    {
                        decimal montoFinal = (docCurrency != "PYG" && docRate != 0) ? montoBase / docRate : montoBase;
                        int tipoPago = transferSum > 0 ? 5 : 1; // Transferencia o Efectivo

                        // Normalizar código de moneda
                        string? codigoMonedaNormalizado = docCurrency switch
                        {
                            "US$" => "USD",
                            "GS" => "PYG",
                            _ => docCurrency
                        };

                        // Traer la descripción de la moneda
                        string queryMoneda = $"Currencies?$select=Name&$filter=DocumentsCode eq '{docCurrency}'";
                        var jsonMoneda = await HttpHelper.GetStringAsync(_httpClient, queryMoneda, _logger, $"Error al obtener descripción de moneda {docCurrency}");

                        string? descripcionMoneda = docCurrency; // valor por defecto

                        if (!string.IsNullOrEmpty(jsonMoneda))
                        {
                            var monedaObj = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonMoneda);
                            if (monedaObj != null && monedaObj.ContainsKey("value"))
                            {
                                var monedas = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(monedaObj["value"].ToString());
                                if (monedas.Any())
                                {
                                    descripcionMoneda = monedas[0]["Name"].ToString();
                                }
                            }
                        }

                        return new GPaConEIni(tipoPago, montoFinal, codigoMonedaNormalizado, descripcionMoneda, codigoMonedaNormalizado != "PYG" ? docRate : (decimal?)null);
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error al obtener pago contado para factura {docEntryFactura}: {ex.Message}");
            return null;
        }
    }
    
    public async Task<List<Factura>> GetFacturasSinAutorizar()
    {
        string queryDocumento = "$crossjoin(Invoices,BusinessPartners,Currencies)" +
            "?$expand=Invoices($select=DocEntry,DocRate,DocType,DocCurrency,U_FE_CDC,U_CENT_TIPO_DOC,CardCode,U_FE_Estado,U_CENT_EST,U_CENT_PE,U_CENT_TIMB,U_FITE,FolioNumber,DocDate,U_iTipTra,U_FE_IndPresencia,PaymentGroupCode," +
            "NumberOfInstallments,U_FE_CODERR,Comments)," +
            "BusinessPartners($select=CardCode,CardName,FederalTaxID,U_CENT_TIP_SN,U_CENT_SITUACION,U_CENT_TIP_OPE,U_CENT_IDENT_TIPO,Phone1,Cellular,EmailAddress)," +
            "Currencies($select=Code,Name,DocumentsCode)" +
            "&$filter=Invoices/CardCode eq BusinessPartners/CardCode and " +
            "Invoices/DocCurrency eq Currencies/Code and " +
            "Invoices/FolioNumber ne null and " +
            "Invoices/DocDate ge '20260201' and " +
            "Invoices/U_FE_Estado ne 'AUT' and Invoices/U_CENT_DECLARABLE eq 'S' and Invoices/Cancelled eq 'tNO' and " +
            "Invoices/U_FE_CDC ne null and Invoices/U_FE_CDC ne '' ";
        //    "Invoices/DocEntry eq 3480";

        var jsonResponse = await HttpHelper.GetStringAsync(_httpClient, queryDocumento, _logger, "Error en la consulta a SAP");
        if (string.IsNullOrEmpty(jsonResponse))
        {
            _logger.LogWarning("No se encontraron datos en la respuesta de SAP.");
            return new List<Factura>();
        }

        var rawJson = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonResponse);
        if (rawJson == null || !rawJson.ContainsKey("value"))
        {
            _logger.LogWarning("No se encontraron datos en la respuesta de SAP.");
            return new List<Factura>();
        }

        // Obtener la lista de facturas y deserializar
        var facturasJson = rawJson["value"].ToString();
        var facturasResponse = JsonConvert.DeserializeObject<List<FacturaResponse>>(facturasJson);

        if (facturasResponse == null)
        {
            _logger.LogWarning("No se pudieron deserializar las facturas.");
            return new List<Factura>();
        }

        // Agrupar las respuestas por DocEntry para consolidar todas las líneas de cada factura
        var facturasAgrupadas = facturasResponse
            .GroupBy(f => f.Invoices.DocEntry)
            .ToDictionary(g => g.Key, g => g.ToList());

        var cardCode = facturasResponse.Select(f => f.BusinessPartners.CardCode).Distinct().ToList();
        var direcciones = await GetDireccionesSocioNegocio(cardCode);
        var paises = direcciones.Select(d => d.Country).Where(c => !string.IsNullOrEmpty(c)).Distinct().ToList();

        // Crear diccionarios para nombres y códigos de países
        var nombresCodigosPaises = new Dictionary<string, (string Nombre, string CodigoReporte)>();

        foreach (var pais in paises)
        {
            var infoCompleta = await GetInformacionPais(pais);
            nombresCodigosPaises[pais] = infoCompleta;
        }

        // Lista final de facturas
        var facturasList = new List<Factura>();

        // Procesar cada factura agrupada con sus líneas
        foreach (var facturaGroup in facturasAgrupadas)
        {
            var docEntry = facturaGroup.Key;
            // Tomar la primera entrada para obtener la información general de la factura
            var primeraEntrada = facturaGroup.Value.First();

            if (primeraEntrada.Invoices == null || primeraEntrada.BusinessPartners == null || primeraEntrada.Currencies == null)
            {
                continue;
            }

            // Encontrar la dirección para este socio de negocio
            var direccion = direcciones.FirstOrDefault(d => d.CardCode == primeraEntrada.BusinessPartners.CardCode);

            // Obtener la información del Socio de negocios
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

            // Crear la factura con los datos generales
            var factura = new Factura
            {
                DocEntry = primeraEntrada.Invoices.DocEntry,
                DocType = primeraEntrada.Invoices.DocType,
                U_FE_CDC = primeraEntrada.Invoices.U_FE_CDC ?? "",
                U_FE_Estado = primeraEntrada.Invoices.U_FE_Estado,
                U_FE_CODERR = primeraEntrada.Invoices.U_FE_CODERR,
                U_CENT_TIPO_DOC = primeraEntrada.Invoices.U_CENT_TIPO_DOC?.PadLeft(2, '0'),
                CardCode = primeraEntrada.Invoices.CardCode ?? "",
                U_CENT_EST = primeraEntrada.Invoices.U_CENT_EST ?? "",
                U_CENT_PE = primeraEntrada.Invoices.U_CENT_PE ?? "",
                FolioNum = primeraEntrada.Invoices.FolioNumber ?? "",
                DocDate = primeraEntrada.Invoices.DocDate,
                DocTime = await ObtenerDocTimePorDocEntry(docEntry),
                U_CENT_TIMB = primeraEntrada.Invoices.U_CENT_TIMB,
                U_FITE = primeraEntrada.Invoices.U_FITE,
                iTipTra = primeraEntrada.Invoices.U_iTipTra,
                iIndPres = primeraEntrada.Invoices.U_FE_IndPresencia,
                iCondOpe = primeraEntrada.Invoices.PaymentGroupCode,
                iCondCred = primeraEntrada.Invoices.NumberOfInstallments,
                dTiCam = primeraEntrada.Invoices.DocRate,
                Comments = primeraEntrada.Invoices.Comments,
                BusinessPartner = new BusinessPartner
                {
                    CardCode = primeraEntrada.BusinessPartners.CardCode ?? "",
                    dNomRec = primeraEntrada.BusinessPartners.CardName ?? "",
                    FederalTaxID = primeraEntrada.BusinessPartners.FederalTaxID ?? "",
                    iTiContRec = primeraEntrada.BusinessPartners.U_CENT_TIP_SN ?? 0,
                    iTiOpe = primeraEntrada.BusinessPartners.U_CENT_TIP_OPE,
                    iNatRec = primeraEntrada.BusinessPartners.U_CENT_SITUACION ?? "",
                    iTipIDRec = primeraEntrada.BusinessPartners.U_CENT_IDENT_TIPO,
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
            await ObtenerLineasFactura(factura, docEntry);
            facturasList.Add(factura);
        }

        // Inicializar operación de crédito y obtener cuotas para facturas a crédito
        foreach (var factura in facturasList)
        {
            // Normalizar la condición de operación y condición de crédito
            int condicionOperacion = factura.iCondOpe == -1 ? 1 : 2;
            int condicionCredito = factura.iCondCred == 1 ? 1 : 2;

            // Solo inicializar la operación de crédito si la condición de operación es crédito (2)
            if (condicionOperacion == 2)
            {
                // Obtener el plazo de crédito según la condición
                string plazoCredito = "";
                if (condicionCredito == 1)
                {
                    plazoCredito = await ObtenerPlazoCredito(factura.DocEntry);
                }

                int? cantidadCuotas = condicionCredito == 2 ? factura.iCondCred : null;

                // Inicializar la operación de crédito
                factura.OperacionCredito = new GPagCred(condicionCredito, plazoCredito, cantidadCuotas);

                // Si es por cuotas, obtener las cuotas
                if (condicionCredito == 2)
                {
                    try
                    {
                        var cuotasResponse = await GetCuotasFactura(factura.DocEntry);

                        if (cuotasResponse != null && cuotasResponse.Any())
                        {
                            foreach (var cuota in cuotasResponse)
                            {
                                if (DateTime.TryParse(cuota.U_FECHAV, out DateTime fechaVencimiento))
                                {
                                    // Determinar el monto de la cuota
                                    decimal montoCuota = cuota.TotalFC > 0 ? cuota.TotalFC : cuota.Total;

                                    var nuevaCuota = new GCuotas(
                                        factura.Currencies.cMoneOpe,         // Moneda de la cuota
                                        factura.Currencies.dDesMoneOpe,      // Descripción de la moneda
                                        montoCuota,                          // Monto de la cuota
                                        fechaVencimiento                     // Fecha de vencimiento
                                    );

                                    factura.OperacionCredito.Cuotas.Add(nuevaCuota);
                                }
                                else
                                {
                                    _logger.LogWarning($"Advertencia: Formato de fecha inválido en cuota: {cuota.U_FECHAV}");
                                }
                            }
                        }
                        else
                        {
                            _logger.LogWarning($"No se encontraron cuotas para la factura {factura.DocEntry}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error al obtener cuotas para DocEntry {factura.DocEntry}: {ex.Message}");
                        if (ex.InnerException != null)
                        {
                            _logger.LogError($"Error interno: {ex.InnerException.Message}");
                        }
                    }
                }
            }

            factura.PagoContado = await GetPagoContado(factura.DocEntry);

        }
        return facturasList;
    }
}