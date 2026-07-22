using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

public class NotaRemisionService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NotaRemisionService> _logger;

    public NotaRemisionService(SAPServiceLayer sapServiceLayer, ILogger<NotaRemisionService> logger)
    {
        _httpClient = sapServiceLayer.GetHttpClient();
        _logger = logger;
    }

    public async Task<List<NotaRemision>> GetNotaRemisionSinCDC()
    {
        // NOTA: se asume que ODLN ya tiene los mismos UDF de facturación electrónica que OINV/ORIN
        // (U_FE_CDC, U_FE_Estado, U_CENT_TIPO_DOC, U_CENT_EST, U_CENT_PE, U_CENT_TIMB, U_FITE). Ajustar filtro/select si difieren.
        string queryDocumento = "$crossjoin(DeliveryNotes,BusinessPartners,Currencies) " +
            "?$expand=DeliveryNotes($select=DocEntry,DocType,DocRate,DocCurrency,U_FE_CDC,U_CENT_TIPO_DOC,CardCode,U_CENT_EST,U_CENT_PE,U_CENT_TIMB,U_FITE,FolioNumber,DocDate,U_FE_CODERR," +
            "U_NUMFC,U_TIMFC,U_NORE,Comments)," +
            "BusinessPartners($select=CardCode,CardName,FederalTaxID,U_TIPCONT,U_CRSI,U_EXX_FE_TipoOperacion,Phone1,Cellular,EmailAddress), " +
            "Currencies($select=Code,Name,DocumentsCode) " +
            "&$filter=DeliveryNotes/CardCode eq BusinessPartners/CardCode and " +
            "DeliveryNotes/DocCurrency eq Currencies/Code and (DeliveryNotes/U_FE_CDC eq null or DeliveryNotes/U_FE_CDC eq '') and DeliveryNotes/U_FE_Estado eq 'NEN' and DeliveryNotes/Cancelled eq 'tNO' and " +
            "DeliveryNotes/DocDate ge '20260201' and DeliveryNotes/FolioNumber ne null";

        return await ProcesarConsultaNotaRemision(queryDocumento);
    }

    public async Task<List<NotaRemision>> GetNotaRemisionSinAutorizar()
    {
        string queryDocumento = "$crossjoin(DeliveryNotes,BusinessPartners,Currencies) " +
            "?$expand=DeliveryNotes($select=DocEntry,DocType,DocRate,DocCurrency,U_FE_CDC,U_CENT_TIPO_DOC,CardCode,U_CENT_EST,U_CENT_PE,U_CENT_TIMB,U_FITE,FolioNumber,DocDate,U_FE_Estado,U_FE_CODERR," +
            "U_NUMFC,U_TIMFC,U_NORE,Comments)," +
            "BusinessPartners($select=CardCode,CardName,FederalTaxID,U_TIPCONT,U_CRSI,U_EXX_FE_TipoOperacion,Phone1,Cellular,EmailAddress), " +
            "Currencies($select=Code,Name,DocumentsCode) " +
            "&$filter=DeliveryNotes/CardCode eq BusinessPartners/CardCode and " +
            "DeliveryNotes/DocCurrency eq Currencies/Code and " +
            "DeliveryNotes/FolioNumber ne null and " +
            "DeliveryNotes/DocDate ge '20260201' and " +
            "DeliveryNotes/U_FE_Estado ne 'AUT' and DeliveryNotes/Cancelled eq 'tNO' and " +
            "DeliveryNotes/U_FE_CDC ne null and DeliveryNotes/U_FE_CDC ne ''";

        return await ProcesarConsultaNotaRemision(queryDocumento);
    }

    private async Task<List<NotaRemision>> ProcesarConsultaNotaRemision(string queryDocumento)
    {
        var jsonResponse = await HttpHelper.GetStringAsync(_httpClient, queryDocumento, _logger, "Error en la consulta a SAP");
        if (string.IsNullOrEmpty(jsonResponse))
        {
            _logger.LogWarning("No se encontraron datos en la respuesta de SAP.");
            return new List<NotaRemision>();
        }

        var rawJson = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonResponse);
        if (rawJson == null || !rawJson.ContainsKey("value"))
        {
            _logger.LogWarning("No se encontraron datos en la respuesta de SAP.");
            return new List<NotaRemision>();
        }

        var notaRemisionJson = rawJson["value"].ToString();
        var notaRemisionResponse = JsonConvert.DeserializeObject<List<NotaRemisionResponse>>(notaRemisionJson);

        if (notaRemisionResponse == null)
        {
            _logger.LogWarning("No se pudieron deserializar las notas de remisión.");
            return new List<NotaRemision>();
        }

        // Agrupar las respuestas por DocEntry para consolidar todas las líneas de cada nota de remisión
        var notaRemisionAgrupadas = notaRemisionResponse
            .GroupBy(n => n.DeliveryNotes.DocEntry)
            .ToDictionary(g => g.Key, g => g.ToList());

        var cardCodes = notaRemisionResponse.Select(f => f.BusinessPartners.CardCode).Distinct().ToList();
        var direcciones = await GetDireccionesSocioNegocio(cardCodes);
        var paises = direcciones.Select(d => d.Country).Where(c => !string.IsNullOrEmpty(c)).Distinct().ToList();

        var nombresCodigosPaises = new Dictionary<string, (string Nombre, string CodigoReporte)>();
        foreach (var pais in paises)
        {
            nombresCodigosPaises[pais] = await GetInformacionPais(pais);
        }

        var notaRemisionList = new List<NotaRemision>();

        foreach (var grupo in notaRemisionAgrupadas)
        {
            var docEntry = grupo.Key;
            var primeraEntrada = grupo.Value.First();

            if (primeraEntrada.DeliveryNotes == null || primeraEntrada.BusinessPartners == null || primeraEntrada.Currencies == null)
            {
                continue;
            }

            var direccion = direcciones.FirstOrDefault(d => d.CardCode == primeraEntrada.BusinessPartners.CardCode);

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

            var notaRemision = new NotaRemision
            {
                DocEntry = primeraEntrada.DeliveryNotes.DocEntry,
                DocType = primeraEntrada.DeliveryNotes.DocType,
                U_FE_CDC = primeraEntrada.DeliveryNotes.U_FE_CDC ?? "",
                U_FE_Estado = primeraEntrada.DeliveryNotes.U_FE_Estado,
                U_FE_CODERR = primeraEntrada.DeliveryNotes.U_FE_CODERR,
                U_CENT_TIPO_DOC = primeraEntrada.DeliveryNotes.U_CENT_TIPO_DOC?.PadLeft(2, '0'),
                CardCode = primeraEntrada.DeliveryNotes.CardCode ?? "",
                U_CENT_EST = primeraEntrada.DeliveryNotes.U_CENT_EST ?? "",
                U_CENT_PE = primeraEntrada.DeliveryNotes.U_CENT_PE ?? "",
                FolioNum = primeraEntrada.DeliveryNotes.FolioNumber ?? "",
                DocDate = primeraEntrada.DeliveryNotes.DocDate,
                DocTime = await ObtenerDocTimePorDocEntry(docEntry),
                U_CENT_TIMB = primeraEntrada.DeliveryNotes.U_CENT_TIMB,
                U_FITE = primeraEntrada.DeliveryNotes.U_FITE,
                dTiCam = primeraEntrada.DeliveryNotes.DocRate,
                U_NUMFC = primeraEntrada.DeliveryNotes.U_NUMFC,
                timbradoSAP = primeraEntrada.DeliveryNotes.U_TIMFC,
                Comments = primeraEntrada.DeliveryNotes.Comments,

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
                    cDepRec = direccion?.CodDepartamento ?? 0,
                    dDesDepRec = direccion?.DescDepartamento ?? "",
                    cDisRec = direccion?.CodDistrito ?? 0,
                    dDesDisRec = direccion?.DescDistrito ?? "",
                    cCiuRec = direccion?.CodCiudad ?? 0,
                    dDesCiuRec = direccion?.DescCiudad ?? "",
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

            string warehouseCode = await ObtenerLineasNotaRemision(notaRemision, docEntry);
            notaRemision.DatosNRE = await ObtenerDatosNRE(primeraEntrada.DeliveryNotes.U_NORE, warehouseCode);

            notaRemisionList.Add(notaRemision);
        }

        return notaRemisionList;
    }

    private async Task<int> ObtenerDocTimePorDocEntry(int docEntry)
    {
        string query = $"DeliveryNotes?$select=DocEntry,DocTime&$filter=DocEntry eq {docEntry}";
        var jsonResponse = await HttpHelper.GetStringAsync(_httpClient, query, _logger, $"Error al obtener DocTime para DocEntry {docEntry}");

        if (string.IsNullOrEmpty(jsonResponse)) return 0;

        var rawJson = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonResponse);
        if (rawJson == null || !rawJson.ContainsKey("value")) return 0;

        var valueJson = rawJson["value"].ToString();
        var notaRemisionDocs = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(valueJson);

        if (notaRemisionDocs == null || notaRemisionDocs.Count == 0) return 0;

        if (notaRemisionDocs[0].ContainsKey("DocTime"))
        {
            var docTimeStr = notaRemisionDocs[0]["DocTime"]?.ToString();

            if (TimeSpan.TryParse(docTimeStr, out var ts))
            {
                return ts.Hours * 10000 + ts.Minutes * 100 + ts.Seconds;
            }
        }

        return 0;
    }

    // Trae las líneas del documento y arma los Items (sin precio ni IVA, no aplica a la NRE).
    // Devuelve el WarehouseCode de la primera línea, usado para resolver el local de salida (gCamSal).
    private async Task<string> ObtenerLineasNotaRemision(NotaRemision notaRemision, int docEntry)
    {
        string queryLineas = $"DeliveryNotes({docEntry})/DocumentLines";
        var responseLineas = await _httpClient.GetAsync(queryLineas);
        string warehouseCode = null;

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
                            warehouseCode ??= linea.WarehouseCode;

                            notaRemision.Items.Add(new Item
                            {
                                dCodInt = linea.ItemCode,
                                dDesProSer = linea.ItemDetails,
                                dCantProSer = linea.Quantity
                            });
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"No se pudieron deserializar las líneas de la Nota de remisión {docEntry}");
                    }
                }
                else
                {
                    _logger.LogWarning($"No se encontraron líneas para la Nota de remisión {docEntry}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error al procesar las líneas para la Nota de remisión {docEntry}: {ex.Message}");
                if (ex.InnerException != null)
                {
                    _logger.LogError($"Error interno: {ex.InnerException.Message}");
                }
            }
        }
        else
        {
            var errorContent = await responseLineas.Content.ReadAsStringAsync();
            _logger.LogError($"Error al obtener líneas para la Nota de remisión {docEntry}: {responseLineas.StatusCode}");
            _logger.LogError($"Detalles: {errorContent}");
        }

        return warehouseCode;
    }

    // @EPY_NRDE + @EPY_TRAN + @EPY_VEHI + OWHS (gCamNRE, gTransp, gCamTrans, gVehTras, gCamSal)
    private async Task<DatosNRE> ObtenerDatosNRE(string codigoNrde, string warehouseCode)
    {
        if (string.IsNullOrEmpty(codigoNrde))
        {
            _logger.LogWarning("El documento no tiene código de @EPY_NRDE (U_NORE) asociado.");
            return null;
        }

        string query = $"EPY_NRDE?$filter=Code eq '{codigoNrde}'";
        var jsonResponse = await HttpHelper.GetStringAsync(_httpClient, query, _logger, $"Error al consultar EPY_NRDE {codigoNrde}");

        if (string.IsNullOrEmpty(jsonResponse)) return null;

        var rawJson = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonResponse);
        if (rawJson == null || !rawJson.ContainsKey("value")) return null;

        var nrdeList = JsonConvert.DeserializeObject<List<EpyNrdeData>>(rawJson["value"].ToString());
        var nrde = nrdeList?.FirstOrDefault();

        if (nrde == null)
        {
            _logger.LogWarning($"No se encontró el registro {codigoNrde} en EPY_NRDE.");
            return null;
        }

        DateTime? fechaFutura = string.IsNullOrWhiteSpace(nrde.U_FEMI) ? null : DateTime.Parse(nrde.U_FEMI);
        DateTime? fechaInicio = string.IsNullOrWhiteSpace(nrde.U_FINI) ? null : DateTime.Parse(nrde.U_FINI);
        DateTime? fechaFin = string.IsNullOrWhiteSpace(nrde.U_FFIN) ? null : DateTime.Parse(nrde.U_FFIN);

        var datosNRE = new DatosNRE
        {
            MotivoEmisionSAP = nrde.U_MEMI,
            ResponsableEmisionSAP = nrde.U_REMI,
            KmRecorrido = nrde.U_KMR,
            FechaFuturaFactura = fechaFutura,
            TipoTransporteSAP = nrde.U_TITRA,
            ModalidadTransporteSAP = nrde.U_MOD,
            ResponsableFleteSAP = nrde.U_REFL,
            CondicionNegociacion = nrde.U_CONE,
            NumeroManifiesto = nrde.U_INFO,
            NumeroDespachoImportacion = nrde.U_DESP,
            FechaInicioTraslado = fechaInicio,
            FechaFinTraslado = fechaFin
        };

        if (!string.IsNullOrEmpty(nrde.U_PAIS))
        {
            var infoPais = await GetInformacionPais(nrde.U_PAIS);
            datosNRE.PaisDestino = infoPais.CodeForReports;
            datosNRE.PaisDestinoDescripcion = infoPais.Name;
        }

        if (!string.IsNullOrEmpty(nrde.U_TRAN))
        {
            datosNRE.Transportista = await ObtenerTransportista(nrde.U_TRAN);
        }

        if (!string.IsNullOrEmpty(nrde.U_VEHI))
        {
            datosNRE.Vehiculo = await ObtenerVehiculo(nrde.U_VEHI);
        }

        if (!string.IsNullOrEmpty(warehouseCode))
        {
            datosNRE.LocalSalida = await ObtenerLocalSalida(warehouseCode);
        }

        return datosNRE;
    }

    private async Task<Transportista> ObtenerTransportista(string codigo)
    {
        string query = $"EPY_TRAN?$filter=Code eq '{codigo}'";
        var jsonResponse = await HttpHelper.GetStringAsync(_httpClient, query, _logger, $"Error al consultar EPY_TRAN {codigo}");

        if (string.IsNullOrEmpty(jsonResponse)) return null;

        var rawJson = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonResponse);
        if (rawJson == null || !rawJson.ContainsKey("value")) return null;

        var tran = JsonConvert.DeserializeObject<List<EpyTranData>>(rawJson["value"].ToString())?.FirstOrDefault();
        if (tran == null)
        {
            _logger.LogWarning($"No se encontró el registro {codigo} en EPY_TRAN.");
            return null;
        }

        return new Transportista
        {
            NaturalezaSAP = tran.U_CRDI,
            TipoDocIdentidadSAP = tran.U_DIDE,
            DireccionAgente = tran.U_DIRA,
            DireccionChofer = tran.U_DIRC,
            DomicilioFiscal = tran.U_DOMT,
            DVAgente = tran.U_DRUC,
            DVTransportista = tran.U_DVEA,
            NombreChofer = tran.U_NACH,
            Nacionalidad = tran.U_NACT,
            NumeroDocChofer = tran.U_NIDC,
            NumeroDocTransportista = tran.U_NIDT,
            NombreAgente = tran.U_NOAG,
            NombreTransportista = tran.U_NOMT,
            RucAgente = tran.U_RUCA,
            RucTransportista = tran.U_RUCT
        };
    }

    private async Task<Vehiculo> ObtenerVehiculo(string codigo)
    {
        string query = $"EPY_VEHI?$filter=Code eq '{codigo}'";
        var jsonResponse = await HttpHelper.GetStringAsync(_httpClient, query, _logger, $"Error al consultar EPY_VEHI {codigo}");

        if (string.IsNullOrEmpty(jsonResponse)) return null;

        var rawJson = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonResponse);
        if (rawJson == null || !rawJson.ContainsKey("value")) return null;

        var vehi = JsonConvert.DeserializeObject<List<EpyVehiData>>(rawJson["value"].ToString())?.FirstOrDefault();
        if (vehi == null)
        {
            _logger.LogWarning($"No se encontró el registro {codigo} en EPY_VEHI.");
            return null;
        }

        return new Vehiculo
        {
            DatosAdicionales = vehi.U_DAVE,
            TipoIdentificacionSAP = vehi.U_IDVE,
            Marca = vehi.U_MARC,
            NumeroIdentificacion = vehi.U_NIDV,
            NumeroMatricula = vehi.U_NRMA,
            NumeroVuelo = vehi.U_NVUE,
            TipoVehiculoSAP = vehi.U_TIVE
        };
    }

    // OWHS - mismo patrón que EmpresaService: código SIFEN ya cargado a mano en U_DEPT/U_DIST/U_BALO
    private async Task<LocalSalida> ObtenerLocalSalida(string warehouseCode)
    {
        string query = $"Warehouses('{warehouseCode}')?$select=WarehouseCode,Street,StreetNo,U_DEPT,U_DIST,U_BALO";
        var jsonResponse = await HttpHelper.GetStringAsync(_httpClient, query, _logger, $"Error al consultar el almacén {warehouseCode}");

        if (string.IsNullOrEmpty(jsonResponse)) return null;

        WarehouseData almacen;
        try
        {
            almacen = JsonConvert.DeserializeObject<WarehouseData>(jsonResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error al procesar el almacén {warehouseCode}: {ex.Message}");
            return null;
        }

        if (almacen == null) return null;

        var localSalida = new LocalSalida
        {
            Direccion = almacen.Street,
            NumeroCasa = almacen.StreetNo,
            CodDepartamento = almacen.U_DEPT,
            CodDistrito = almacen.U_DIST,
            CodCiudad = almacen.U_BALO
        };

        localSalida.DescDepartamento = await ObtenerDescripcionGeografica("EPY_DPTO", almacen.U_DEPT, "U_NDEP", "departamento");
        localSalida.DescDistrito = await ObtenerDescripcionGeografica("EPY_DIST", almacen.U_DIST, "U_NCIU", "distrito");
        localSalida.DescCiudad = await ObtenerDescripcionGeografica("EPY_BALO", almacen.U_BALO, "U_NLOC", "localidad");

        return localSalida;
    }

    // Mismo patrón que EmpresaService.ObtenerDescripcionGeografica (código SIFEN -> descripción)
    private async Task<string> ObtenerDescripcionGeografica(string entidad, string codigo, string campoDescripcion, string tipo)
    {
        if (string.IsNullOrEmpty(codigo))
        {
            return "";
        }

        string query = $"{entidad}?$select=Code,{campoDescripcion}&$filter=Code eq '{codigo}'";
        var jsonResponse = await HttpHelper.GetStringAsync(_httpClient, query, _logger, $"Error al consultar {tipo}");

        if (string.IsNullOrEmpty(jsonResponse)) return "";

        try
        {
            dynamic respuesta = JsonConvert.DeserializeObject(jsonResponse);

            if (respuesta?.value != null && respuesta.value.Count > 0)
            {
                return Convert.ToString(respuesta.value[0][campoDescripcion]);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error al procesar la respuesta de {tipo}: {ex.Message}");
        }

        return "";
    }

    // Direcciones del receptor, incluyendo los códigos de departamento/distrito/ciudad (U_EXX_FE_DEPT/DIST/BALO)
    // usados para el local de entrega (gCamEnt)
    public async Task<List<DireccionReceptor>> GetDireccionesSocioNegocio(List<string> cardCodes)
    {
        var direcciones = new List<DireccionReceptor>();

        foreach (var cardCode in cardCodes)
        {
            try
            {
                string queryDirecciones = $"BusinessPartners('{cardCode}')/BPAddresses";
                var jsonResponse = await HttpHelper.GetStringAsync(_httpClient, queryDirecciones, _logger, $"Error al obtener direcciones para {cardCode}");

                if (string.IsNullOrEmpty(jsonResponse))
                {
                    continue;
                }

                var responseObj = JsonConvert.DeserializeObject<BPAddressesWrapper>(jsonResponse);

                if (responseObj == null || responseObj.BPAddresses == null || !responseObj.BPAddresses.Any())
                {
                    _logger.LogWarning($"No se encontraron direcciones para {cardCode}.");
                    continue;
                }

                var primeraDireccion = responseObj.BPAddresses.FirstOrDefault();

                if (primeraDireccion != null)
                {
                    var direccion = new DireccionReceptor
                    {
                        CardCode = cardCode,
                        Country = primeraDireccion.Country ?? "",
                        Street = primeraDireccion.Street ?? "",
                        StreetNo = primeraDireccion.StreetNo,
                        CodDepartamento = primeraDireccion.U_EXX_FE_DEPT ?? 0,
                        CodDistrito = primeraDireccion.U_EXX_FE_DIST ?? 0,
                        CodCiudad = primeraDireccion.U_EXX_FE_BALO ?? 0
                    };

                    if (direccion.CodDepartamento > 0)
                    {
                        direccion.DescDepartamento = await ObtenerDescripcionGeografica("EPY_DPTO", direccion.CodDepartamento.ToString(), "U_NDEP", "departamento");
                    }

                    if (direccion.CodDistrito > 0)
                    {
                        direccion.DescDistrito = await ObtenerDescripcionGeografica("EPY_DIST", direccion.CodDistrito.ToString(), "U_NCIU", "distrito");
                    }

                    if (direccion.CodCiudad > 0)
                    {
                        direccion.DescCiudad = await ObtenerDescripcionGeografica("EPY_BALO", direccion.CodCiudad.ToString(), "U_NLOC", "localidad");
                    }

                    direcciones.Add(direccion);
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

    // Resuelve el CDC de la Factura referenciada como documento asociado opcional de la NRE (mismo patrón que NotaCreditoService.ObtenerCDCFactura)
    public async Task<string> ObtenerCDCFactura(string dEstDocAso, string dPExpDocAso, string dNumDocAso, string rucCompleto, int? timbradoSAP)
    {
        try
        {
            string query = $"Invoices?$select=U_FE_CDC&$filter=FederalTaxID eq '{rucCompleto}' and U_CENT_EST eq '{dEstDocAso}' and U_CENT_PE eq '{dPExpDocAso}' and FolioNumber eq {dNumDocAso} and U_CENT_TIMB eq '{timbradoSAP}'";
            var jsonResponse = await HttpHelper.GetStringAsync(_httpClient, query, _logger, "Error al obtener datos de factura referenciada");

            if (string.IsNullOrWhiteSpace(jsonResponse))
                return null;

            var root = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonResponse);
            if (root == null || !root.ContainsKey("value"))
                return null;

            var valueArray = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(root["value"].ToString());
            if (valueArray == null || valueArray.Count == 0)
                return null;

            var factura = valueArray[0];
            return factura.ContainsKey("U_FE_CDC") ? factura["U_FE_CDC"]?.ToString() : null;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error al obtener CDC de factura referenciada: {ex.Message}");
            return null;
        }
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

            var valueArray = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(responseObj["value"].ToString());

            if (valueArray == null || valueArray.Count == 0)
            {
                _logger.LogWarning($"No se encontró información para el país {codigoPais}");
                return ("", "");
            }

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
}

// Dirección del receptor, con los códigos SIFEN de depto/distrito/ciudad para el local de entrega (gCamEnt)
public class DireccionReceptor
{
    public string CardCode { get; set; }
    public string Country { get; set; }
    public string Street { get; set; }
    public int? StreetNo { get; set; }
    public int CodDepartamento { get; set; }
    public string DescDepartamento { get; set; }
    public int CodDistrito { get; set; }
    public string DescDistrito { get; set; }
    public int CodCiudad { get; set; }
    public string DescCiudad { get; set; }
}
