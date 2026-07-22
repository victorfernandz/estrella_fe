using System.Text;
using Microsoft.Extensions.Logging;
using System.IO.Compression;
using System.Security.Cryptography.X509Certificates;
using System.Net.Http.Headers;
using System.Security.Authentication;
using Newtonsoft.Json;
using System.Xml;
public class EnvioSifenService
{
    private HttpClient _httpClient;
    private readonly LoggerSifenService _logger;
    private readonly string _baseDatos;
    private readonly ILogger _log;
    private readonly SAPServiceLayer _sapServiceLayer;
    private readonly SifenConfig _sifenConfig;
    private readonly EmailQueueRepository _emailQueue;

    public EnvioSifenService(string baseUrl, LoggerSifenService logger, Config config, ILogger log, SAPServiceLayer sapServiceLayer = null, EmailQueueRepository emailQueue = null)
    {
        _logger = logger;
        _baseDatos = config.SapServiceLayerList[0].CompanyDB;
        _sifenConfig = config.SapServiceLayerList[0].Sifen;

        _log = log;
        _sapServiceLayer = sapServiceLayer;
        _emailQueue = emailQueue;

        var handler = new HttpClientHandler
        {
            SslProtocols = SslProtocols.Tls12,
            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
        };

        _httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromMinutes(10)
        };

        _log.LogInformation($"EnvioSifenService inicializado con URL base: {baseUrl}");
    }

    public static string NormalizarXmlFirmado(string xmlFirmado, bool quitarDeclaracionXml = true)
    {
        if (string.IsNullOrWhiteSpace(xmlFirmado))
            return xmlFirmado;

        if (quitarDeclaracionXml && xmlFirmado.TrimStart().StartsWith("<?xml"))
        {
            int endDeclaration = xmlFirmado.IndexOf("?>");
            if (endDeclaration > 0)
                xmlFirmado = xmlFirmado.Substring(endDeclaration + 2).TrimStart();
        }

        XmlDocument xmlDoc = new XmlDocument();
        xmlDoc.PreserveWhitespace = true;
        xmlDoc.LoadXml(xmlFirmado);

        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = false,
            OmitXmlDeclaration = quitarDeclaracionXml,
            NewLineHandling = NewLineHandling.None,
            CheckCharacters = false
        };

        using (var ms = new MemoryStream())
        using (var writer = XmlWriter.Create(ms, settings))
        {
            xmlDoc.Save(writer);
            writer.Flush();
            ms.Position = 0;
            return Encoding.UTF8.GetString(ms.ToArray()).TrimStart('\r', '\n');
        }
    }

    public async Task<string> EnviarDocumentoAsincronico(List<(int docEntry, string cdc, string xmlFirmado)> documentosFirmados, string tipoDocumento, string xmlTiDE)
    {
        if (documentosFirmados.Count < 1 || documentosFirmados.Count > 50)
            throw new Exception($"El lote debe contener entre 1 y 50 documentos. Cantidad encontrada: {documentosFirmados.Count}");

        var fechaCreacion = DateTime.Now;
        var fechaEnvio = DateTime.Now;
        string dId = DateTime.Now.ToString("yyyyMMddHHmmssfff").Substring(0, 15);
        string debugDir = "debug_xml";
        Directory.CreateDirectory(debugDir);
        string numeroLote = "";
        string codigoRespuesta = "";
        
        try
        {
            if (_sapServiceLayer != null)
            {
                try
                {
                    var (certificadoBytes, password) = await ObtenerCertificadoActivo();
                    ConfigurarCertificadoCliente(certificadoBytes, password);
                }
                catch (Exception certEx)
                {
                    _log.LogWarning($"No se pudo obtener el certificado TLS: {certEx.Message}");
                }
            }

            // Crear XML del lote
            XmlDocument loteDoc = new XmlDocument();
            loteDoc.PreserveWhitespace = true;
            XmlDeclaration xmlDecl = loteDoc.CreateXmlDeclaration("1.0", "utf-8", null);
            loteDoc.AppendChild(xmlDecl);

            XmlElement rootElement = loteDoc.CreateElement("rLoteDE", "");
            loteDoc.AppendChild(rootElement);

            foreach (var (docEntry, cdc, xmlFirmado) in documentosFirmados)
            {
                try
                {
                    string basePath = AppDomain.CurrentDomain.BaseDirectory; 
                    string rutaArchivoFirmado = Path.Combine(basePath, "XML", _baseDatos, $"Documento_{cdc}.xml");
                    if (!File.Exists(rutaArchivoFirmado))
                    {
                        throw new FileNotFoundException($"No se encontró el archivo firmado para CDC {cdc} en {rutaArchivoFirmado}");
                    }

                    File.Copy(rutaArchivoFirmado, Path.Combine(debugDir, $"rDE_completo_{cdc}_{DateTime.Now:yyyyMMddHHmmss}.xml"), true);

                    string xmlNormalizado = NormalizarXmlFirmado(xmlFirmado);
                    XmlDocument docXml = new XmlDocument();
                    docXml.PreserveWhitespace = true;
                    docXml.LoadXml(xmlNormalizado);

                    XmlNode rdeImportado = loteDoc.ImportNode(docXml.DocumentElement, true);
                    rootElement.AppendChild(rdeImportado);
                }
                catch (Exception ex)
                {
                    _log.LogError($"Error al procesar XML para CDC {cdc}: {ex.Message}");
                    throw;
                }
            }

            // Guardar el lote en memoria
            MemoryStream loteStream = new MemoryStream();
            XmlWriterSettings settings = new XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                Encoding = new UTF8Encoding(false)
            };
            using (XmlWriter writer = XmlWriter.Create(loteStream, settings))
            {
                loteDoc.Save(writer);
            }
            loteStream.Position = 0;

            string loteXmlDebug = Encoding.UTF8.GetString(loteStream.ToArray());
            File.WriteAllText(Path.Combine(debugDir, $"rLoteDE_zip_contenido_{dId}.xml"), loteXmlDebug);

            // Comprimir ZIP
            string zipPath = Path.Combine(debugDir, $"rLoteDE_lote_{dId}.zip");
            if (File.Exists(zipPath))
                File.Delete(zipPath);

            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                var entry = zip.CreateEntry("rLoteDE.xml", CompressionLevel.Optimal);
                using (var entryStream = entry.Open())
                {
                    loteStream.CopyTo(entryStream);
                }
            }

            // Codificar en base64
            byte[] zipBytes = File.ReadAllBytes(zipPath);
            string base64Zip = Convert.ToBase64String(zipBytes);
            File.WriteAllText(Path.Combine(debugDir, $"lote_base64_{dId}.txt"), base64Zip);

            // Construir SOAP
            string sb = $@"<soap:Envelope xmlns:soap=""http://www.w3.org/2003/05/soap-envelope"" xmlns:xsd=""http://ekuatia.set.gov.py/sifen/xsd""><soap:Header/>
    <soap:Body><xsd:rEnvioLote>
            <xsd:dId>{dId}</xsd:dId>
            <xsd:xDE>{base64Zip}</xsd:xDE>
        </xsd:rEnvioLote>
    </soap:Body>
</soap:Envelope>";

            string soapEnvelope = sb.ToString();
            File.WriteAllText(Path.Combine(debugDir, $"soap_request_lote_{dId}.xml"), soapEnvelope);

            var soapContent = new StringContent(soapEnvelope, new UTF8Encoding(false), "text/xml");
            soapContent.Headers.Add("SOAPAction", "");
            soapContent.Headers.ContentType.Parameters.Clear();
            soapContent.Headers.ContentType.Parameters.Add(new NameValueHeaderValue("charset", "UTF-8"));

        //    string fullUrl = "de/ws/async/recibe-lote.wsdl";

            string fullUrl;
            var baseAddr = _httpClient?.BaseAddress?.ToString() ?? "";

            if (baseAddr.ToLower().Contains("test"))
            {
                fullUrl = "https://sifen-test.set.gov.py/de/ws/async/recibe-lote.wsdl";
            }
            else
            {
                fullUrl = "de/ws/async/recibe-lote.wsdl";
            }

            var response = await _httpClient.PostAsync(fullUrl, soapContent);
            string mensajeRespuesta = await response.Content.ReadAsStringAsync();
            File.WriteAllText(Path.Combine(debugDir, $"soap_response_lote_{dId}_{DateTime.Now:yyyyMMddHHmmss}.xml"), mensajeRespuesta);
          
            string estado = response.IsSuccessStatusCode ? "Enviado" : "Error";
            DateTime? fechaRespuesta = DateTime.Now;

            try
            {
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(mensajeRespuesta);
                var ns = new XmlNamespaceManager(xmlDoc.NameTable);
                ns.AddNamespace("ns2", "http://ekuatia.set.gov.py/sifen/xsd");

                string? codRes = xmlDoc.SelectSingleNode("//ns2:dCodRes", ns)?.InnerText;
                string? nroLote = xmlDoc.SelectSingleNode("//ns2:dProtConsLote", ns)?.InnerText;
                string? mensaje = xmlDoc.SelectSingleNode("//ns2:dMsgRes", ns)?.InnerText;

                if (!string.IsNullOrEmpty(codRes)) codigoRespuesta = codRes;
                if (!string.IsNullOrEmpty(nroLote)) numeroLote = nroLote;

                _log.LogInformation($"Código respuesta: {codigoRespuesta}, Número lote: {numeroLote}");

                bool esTest = baseAddr?.Contains("test", StringComparison.OrdinalIgnoreCase) == true;

                foreach (var (docEntry, cdc, xmlFirmado) in documentosFirmados)
                {
                    try
                    {
                    //    if (!esTest)
                    //    {
                            _logger.RegistrarDocumento(_baseDatos, cdc, dId, numeroLote, xmlFirmado, estado, tipoDocumento, "siRecepLoteDE", fechaCreacion, fechaEnvio, fechaRespuesta, mensaje, codigoRespuesta);   
                    //    }
                        
                        if (_sapServiceLayer != null)
                        {
                            if (docEntry != -1)
                            {
                                XmlDocument firmadoDoc = new XmlDocument();
                                firmadoDoc.PreserveWhitespace = true;
                                firmadoDoc.LoadXml(xmlFirmado);

                                string? qr = firmadoDoc.GetElementsByTagName("dCarQR")?[0]?.InnerText;

                                bool actualizado = await ActualizarDocumento(xmlTiDE, docEntry, cdc, estado, codigoRespuesta, mensaje, qr);

                                _log.LogInformation($"Documento {tipoDocumento} con CDC {cdc} actualizado en SAP: {actualizado}");
                            }
                            else
                            {
                                _log.LogWarning($"No se encontró docEntry para CDC {cdc}, tipo {tipoDocumento}");
                            }
                        }
                    }
                    catch (Exception dbEx)
                    {
                        _log.LogError($"Error al registrar CDC {cdc}: {dbEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning($"Error al analizar respuesta XML: {ex.Message}");
            }

            if (!string.IsNullOrEmpty(numeroLote))
            {
                //bool consultaExitosa = await ConsultarEstadoLoteAsync(dId, numeroLote);
                bool consultaExitosa = await ConsultarEstadoLoteAsync(dId, numeroLote, documentosFirmados, tipoDocumento, fechaCreacion, fechaEnvio);
                _log.LogInformation($"Consulta de estado del lote {numeroLote} finalizada: {(consultaExitosa ? "Éxito" : "Incompleta o con error")}");
            }

            return numeroLote;

        }
        catch (Exception ex)
        {
            _log.LogError($"Error al enviar lote: {ex.Message}");
            if (ex.InnerException != null)
            {
                _log.LogError($"Error interno: {ex.InnerException.Message}");
            }
            throw;
        }
    }

    public async Task<bool> ConsultarEstadoLoteAsync(string dId, string numeroLote, List<(int docEntry, string cdc, string xmlFirmado)> documentosFirmados, string tipoDocumento, DateTime fechaCreacion, DateTime fechaEnvio)
    {
        try
        {
            _log.LogInformation($"Consultando estado del lote con dId: {dId}, nroLote: {numeroLote}");

            string soapRequest = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
    <soap:Envelope xmlns:soap=""http://www.w3.org/2003/05/soap-envelope"">
    <soap:Header/>
    <soap:Body>
        <rEnviConsLoteDe xmlns=""http://ekuatia.set.gov.py/sifen/xsd"">
        <dId>{dId}</dId>
        <dProtConsLote>{numeroLote}</dProtConsLote>
        </rEnviConsLoteDe>
    </soap:Body>
    </soap:Envelope>";

            var content = new StringContent(soapRequest, Encoding.UTF8, "application/soap+xml");
            content.Headers.ContentType.Parameters.Clear();
            content.Headers.ContentType.Parameters.Add(new NameValueHeaderValue("charset", "UTF-8"));

            string endpoint;
            var baseAddr = _httpClient?.BaseAddress?.ToString() ?? "";

            if (baseAddr.ToLower().Contains("test"))
            {
                endpoint = "https://sifen-test.set.gov.py/de/ws/consultas/consulta-lote.wsdl";
            }
            else
            {
                endpoint = "de/ws/consultas/consulta-lote.wsdl";
            }

         //   string endpoint = "de/ws/consultas/consulta-lote.wsdl";

            var response = await _httpClient.PostAsync(endpoint, content);
            var resultXml = await response.Content.ReadAsStringAsync();

            string debugDir = "debug_xml";
            File.WriteAllText(Path.Combine(debugDir, $"soap_consulta_rEnviConsLoteDe_{numeroLote}_{DateTime.Now:yyyyMMddHHmmss}.xml"), resultXml);

            if (response.IsSuccessStatusCode)
            {
                if (!resultXml.TrimStart().StartsWith("<"))
                {
                    _log.LogError("La respuesta del servicio no es XML válido:");
                    _log.LogError(resultXml);
                    return false;
                }

                try
                {
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.LoadXml(resultXml);

                    var ns = new XmlNamespaceManager(xmlDoc.NameTable);
                    ns.AddNamespace("env", "http://www.w3.org/2003/05/soap-envelope");
                    ns.AddNamespace("ns2", "http://ekuatia.set.gov.py/sifen/xsd");

                    string? estado = xmlDoc.SelectSingleNode("//ns2:dEstRes", ns)?.InnerText;
                    string? codigo = xmlDoc.SelectSingleNode("//ns2:dCodResLot", ns)?.InnerText;
                    string? mensaje = "";

                    if (codigo == "0361")
                    {
                        estado = "Enviado";
                        codigo = xmlDoc.SelectSingleNode("//ns2:dCodResLot", ns)?.InnerText;
                        mensaje = xmlDoc.SelectSingleNode("//ns2:dMsgResLot", ns)?.InnerText;
                    }
                    else
                    {
                        codigo = xmlDoc.SelectSingleNode("//ns2:dCodRes", ns)?.InnerText;
                        mensaje = xmlDoc.SelectSingleNode("//ns2:dMsgRes", ns)?.InnerText;

                        _log.LogInformation($"Resultado consulta lote - Estado: {estado}, Código: {codigo}, Mensaje: {mensaje}");
                    }

                    DateTime? fechaRespuesta = DateTime.Now;

                    foreach (var (docEntry, cdc, xmlFirmado) in documentosFirmados)
                    {
                        try
                        {
                            _logger.RegistrarDocumento(_baseDatos, cdc, dId, numeroLote, xmlFirmado, estado, tipoDocumento, "consultaLoteDE", fechaCreacion, fechaEnvio, fechaRespuesta, mensaje, codigo);

                            if (_sapServiceLayer != null && docEntry != -1)
                            {
                                XmlDocument firmadoDoc = new XmlDocument();
                                firmadoDoc.PreserveWhitespace = true;
                                firmadoDoc.LoadXml(xmlFirmado);

                                string? qr = firmadoDoc.GetElementsByTagName("dCarQR")?[0]?.InnerText;
                                bool actualizado = await ActualizarDocumento(tipoDocumento, docEntry, cdc, estado, codigo, mensaje, qr);
                                _log.LogInformation($"Documento {tipoDocumento} con CDC {cdc} actualizado tras consulta: {actualizado}");

                                if (actualizado && (estado == "Aprobado" || estado == "Aprobado con Observaciones") && _emailQueue != null)
                                {
                                    // Namespace wildcard "*" — XMLs SIFEN están namespaced
                                    string? emailRec  = firmadoDoc.GetElementsByTagName("dEmailRec", "*")?[0]?.InnerText
                                                     ?? firmadoDoc.GetElementsByTagName("dEmailRec")?[0]?.InnerText;
                                    string? nombreRec = firmadoDoc.GetElementsByTagName("dNomRec", "*")?[0]?.InnerText
                                                     ?? firmadoDoc.GetElementsByTagName("dNomRec")?[0]?.InnerText;
                                    string basePath = AppDomain.CurrentDomain.BaseDirectory;
                                    string rutaXmlFirmado = Path.Combine(basePath, "XML", _baseDatos, $"Documento_{cdc}.xml");
                                    _emailQueue.Enqueue(_baseDatos, cdc, docEntry, tipoDocumento, emailRec, nombreRec ?? "", qr, rutaXmlFirmado);
                                }
                            }
                        }
                        catch (Exception exReg)
                        {
                            _log.LogError($"Error al registrar resultado de consulta para CDC {cdc}: {exReg.Message}");
                        }
                    }

                    return estado == "Aprobado" || estado == "Rechazado" || estado == "Aprobado con Observaciones";
                }
                catch (XmlException xex)
                {
                    _log.LogError($"La respuesta no es un XML válido. Excepción: {xex.Message}");
                    _log.LogError(resultXml);
                    return false;
                }
            }
            else
            {
                _log.LogWarning($"Error HTTP en consulta con rEnviConsLoteDe: {response.StatusCode}");
                _log.LogWarning(resultXml);
                return false;
            }
        }
        catch (Exception ex)
        {
            _log.LogError($"Error al consultar con rEnviConsLoteDe: {ex.Message}");
            return false;
        }
    }

    public async Task<(byte[] certificadoBytes, string contraseña)> ObtenerCertificadoActivo()
    {
        try
        {
            if (_sapServiceLayer == null)
            {
                throw new InvalidOperationException("SAPServiceLayer no está disponible para obtener el certificado");
            }

            // Consultar el certificado activo
            string query = "U_CERTIFICADOS?$filter=U_ACTIVO eq 'Y'";

            var response = await _sapServiceLayer.GetHttpClient().GetAsync(query);

            if (!response.IsSuccessStatusCode)
            {
                _log.LogError($"Error al consultar certificados: {response.StatusCode}");
                throw new Exception($"Error al consultar certificados: {response.StatusCode}");
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrEmpty(jsonResponse))
            {
                throw new Exception("No se pudo obtener respuesta del servicio de certificados");
            }

            var responseObj = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonResponse);

            if (responseObj == null || !responseObj.ContainsKey("value"))
            {
                throw new Exception("Formato de respuesta inválido al obtener certificado");
            }

            var certificadosArray = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(responseObj["value"].ToString());

            if (certificadosArray == null || certificadosArray.Count == 0)
            {
                throw new Exception("No se encontró un certificado activo");
            }

            // Tomar el primer certificado activo
            var certificado = certificadosArray[0];

            // Obtener los datos del certificado y contraseña
            string? certificadoBase64 = certificado["U_ARCHIVO"].ToString();
            string? contraseñaBase64 = certificado["U_PWD"].ToString();
            string? baseDatos = certificado["U_BASE_DATOS"].ToString();

            byte[] certificadoBytes = Convert.FromBase64String(certificadoBase64);
            string contraseña = Encoding.UTF8.GetString(Convert.FromBase64String(contraseñaBase64));

        //    _log.LogInformation($"Certificado obtenido correctamente: {certificado["Name"]} - para la base {baseDatos}");
            return (certificadoBytes, contraseña);
        }
        catch (Exception ex)
        {
            _log.LogError($"Error al obtener certificado: {ex.Message}");
            if (ex.InnerException != null)
            {
                _log.LogError($"Error interno: {ex.InnerException.Message}");
            }
            throw new Exception("Error al obtener certificado digital", ex);
        }
    }

    public void ConfigurarCertificadoCliente(byte[] certificadoBytes, string contraseña)
    {
        try
        {
            // Cargar el certificado
            var certificado = new X509Certificate2(certificadoBytes, contraseña, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);

            var handler = new HttpClientHandler
            {
                SslProtocols = SslProtocols.Tls12,
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true,
                ClientCertificates = { certificado }
            };

            var nuevoHttpClient = new HttpClient(handler)
            {
                BaseAddress = _httpClient.BaseAddress,
                Timeout = _httpClient.Timeout
            };

            foreach (var header in _httpClient.DefaultRequestHeaders)
            {
                nuevoHttpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
            }

            var clienteAnterior = _httpClient;
            _httpClient = nuevoHttpClient;

            clienteAnterior.Dispose();
        }
        catch (Exception ex)
        {
            _log.LogError($"Error al configurar certificado cliente: {ex.Message}");
            throw;
        }
    }
    
    public async Task<bool> ActualizarDocumento(string xmlTiDE, int docEntry, string cdc, string estadoSifen, string codigoRespuesta, string descripcionRespuesta, string? QR)
    {
        string estadoInternoSAP = estadoSifen.ToUpper() switch
        {
            "ENVIADO" => "ENV",
            "RECHAZADO" => "NAU",
            "APROBADO" => "AUT",
            "OFFLINE" => "OFF",
            null => "OFF"
        };
        
        DateTime? fechaAutorizacion = null;

        if (estadoInternoSAP == "AUT")
        {
            fechaAutorizacion = DateTime.Now;
        }

        var requestBody = new
        {
            U_EXX_FE_CDC = cdc,
            U_EXX_FE_Estado = estadoInternoSAP,
            U_EXX_FE_CODERR = codigoRespuesta,
            U_EXX_FE_DESERR = descripcionRespuesta,
            U_EXX_FE_FECAUT = fechaAutorizacion,
            U_EXX_FE_QR = QR
        };

        var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

        HttpClient sapClient = _sapServiceLayer.GetHttpClient();

        HttpResponseMessage response;

        if (xmlTiDE == "1" || xmlTiDE == "01")
        {
            response = await sapClient.PatchAsync($"Invoices({docEntry})", content);
        }
        else if (xmlTiDE == "7" || xmlTiDE == "07")
        {
            response = await sapClient.PatchAsync($"DeliveryNotes({docEntry})", content);
        }
        else
        {
            response = await sapClient.PatchAsync($"CreditNotes({docEntry})", content);
        }

        if (!response.IsSuccessStatusCode)
        {
            string errorContent = await response.Content.ReadAsStringAsync();
            _log.LogError($"Error al actualizar documento {docEntry} en SAP. Estado: {estadoSifen}, Código: {estadoInternoSAP}");
            _log.LogError($"Respuesta del Service Layer: {errorContent}");
        }

        return response.IsSuccessStatusCode;
    }

    public async Task<string>  EnviarEventosAsync(List<(int docEntry, string cdc, string xmlFirmado)> documentosFirmados, int tipoDoc)
    {
        var (docEntry, cdc, xmlEvento) = documentosFirmados[0];
        string debugDir = "debug_xml_eventos";
        Directory.CreateDirectory(debugDir);
        string codigoRespuesta = "";
        string numeroTransaccion = "";
    //    string metodoSifen = "siRecepEvento";
        
        try
        {
            if (_sapServiceLayer != null)
            {
                try
                {
                    var (certificadoBytes, password) = await ObtenerCertificadoActivo();
                    ConfigurarCertificadoCliente(certificadoBytes, password);
                }
                catch (Exception certEx)
                {
                    _log.LogWarning($"[Eventos] No se pudo obtener el certificado TLS: {certEx.Message}");
                }
            }

            // Normalizar el XML
            string xmlNormalizado = NormalizarXmlFirmado(xmlEvento);

            // Guardar rEnviEventoDe para debug
            File.WriteAllText(Path.Combine(debugDir, $"rEnviEventoDe_{DateTime.Now:yyyyMMddHHmmss}.xml"),xmlNormalizado);
            //Preparar el SOAP
            string soapEnvelope =
    $@"<soap:Envelope xmlns:soap=""http://www.w3.org/2003/05/soap-envelope"">
    <soap:Header/>
    <soap:Body>
        {xmlNormalizado}
    </soap:Body>
    </soap:Envelope>";

            File.WriteAllText(Path.Combine(debugDir, $"soap_request_eventos_{DateTime.Now:yyyyMMddHHmmss}.xml"),soapEnvelope);
            var soapContent = new StringContent(soapEnvelope, new UTF8Encoding(false), "application/soap+xml");
            soapContent.Headers.ContentType.Parameters.Clear();
            soapContent.Headers.ContentType.Parameters.Add(new NameValueHeaderValue("charset", "UTF-8"));

        //    string fullUrl = "de/ws/eventos/evento.wsdl";
            string fullUrl;
            var baseAddr = _httpClient?.BaseAddress?.ToString() ?? "";

            if (baseAddr.ToLower().Contains("test"))
            {
                fullUrl = "de/ws/eventos/evento.wsdl";
            }
            else
            {
                fullUrl = "de/ws/eventos/evento.wsdl";
            }

            var response = await _httpClient.PostAsync(fullUrl, soapContent);
            string mensajeRespuesta = await response.Content.ReadAsStringAsync();
            File.WriteAllText(Path.Combine(debugDir, $"soap_response_eventos_{DateTime.Now:yyyyMMddHHmmss}.xml"),mensajeRespuesta);
            DateTime? fechaRespuesta = DateTime.Now;

            try
            {
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(mensajeRespuesta);

                var nsManager = new XmlNamespaceManager(xmlDoc.NameTable);
                nsManager.AddNamespace("ns2", "http://ekuatia.set.gov.py/sifen/xsd");

                string? codRes = xmlDoc.SelectSingleNode("//ns2:dCodRes", nsManager)?.InnerText;
                string? mensaje = xmlDoc.SelectSingleNode("//ns2:dMsgRes", nsManager)?.InnerText;
                string? estres = xmlDoc.SelectSingleNode("//ns2:dEstRes", nsManager)?.InnerText;

                if (!string.IsNullOrEmpty(codRes)) codigoRespuesta = codRes;

                _log.LogInformation($"[Eventos] Código respuesta: {codigoRespuesta}, ProtAut: {numeroTransaccion}, Mensaje: {mensaje}");
            //    _logger.RegistrarDocumento(_baseDatos, cdc, "", numeroTransaccion, xmlEvento, estado, tipoDocumento, metodoSifen, DateTime.Now, DateTime.Now, fechaRespuesta, mensaje, codigoRespuesta);

                if (_sapServiceLayer != null)
                {
                    if (docEntry != -1)
                    {
                        XmlDocument firmadoDoc = new XmlDocument();
                        firmadoDoc.PreserveWhitespace = true;
                        firmadoDoc.LoadXml(xmlEvento);

                        bool actualizado = await ActualizarDocEvento(docEntry, estres, codigoRespuesta, mensaje, xmlNormalizado, mensajeRespuesta, tipoDoc);
                        _log.LogInformation($"[Eventos] Documento con DocEntry {docEntry} actualizado en SAP: {actualizado}");
                    }
                    else
                    {
                        _log.LogWarning($"[Eventos] No se encontró docEntry");
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning($"[Eventos] Error al analizar respuesta XML de eventos: {ex.Message}");
            }

            return !string.IsNullOrEmpty(numeroTransaccion) ? numeroTransaccion : codigoRespuesta;
        }
        catch (Exception ex)
        {
            _log.LogError($"[Eventos] Error al enviar eventos: {ex.Message}");
            if (ex.InnerException != null)
            {
                _log.LogError($"[Eventos] Error interno: {ex.InnerException.Message}");
            }
            throw;
        }
    }

    public async Task<bool> ActualizarDocEvento(int docEntry, string estadoSifen, string codigoRespuesta, string descripcionRespuesta, string xmlEvento, string mensajeRespuesta, int tipoDoc)
    {
        string estadoInternoSAP = estadoSifen.ToUpper() switch
        {
            "ENVIADO" => "ENV",
            "RECHAZADO" => "NAU",
            "APROBADO" => "AUT",
            "OFFLINE" => "OFF",
            null => "OFF"
        };
        
        DateTime? fechaAutorizacion = null;

        if (estadoInternoSAP == "AUT")
        {
            fechaAutorizacion = DateTime.Now;
        }

        HttpClient sapClient = _sapServiceLayer.GetHttpClient();
        HttpResponseMessage response;

        if (tipoDoc == 2)
        {
            // Inutilización de numeración
            var requestBody = new
            {
                U_EXX_FE_INUTILIZA_ESTADO = estadoInternoSAP,
                U_EXX_FE_INUTILIZA_CODERR = codigoRespuesta,
                U_EXX_FE_INUTILIZA_DESERR = descripcionRespuesta,
                U_EXX_FE_INUTILIZA_FECHA = fechaAutorizacion,
                U_EXX_FE_INUTILIZA_GEN = xmlEvento,
                U_EXX_FE_INUTILIZA_RESP = mensajeRespuesta
            };

            var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
            response = await sapClient.PatchAsync($"EPY_DVAN({docEntry})", content);
        }
        else
        {
            // Cancelación: Factura (1), Nota de Crédito/Débito (5/6), Nota de Remisión (7)
            string entidad = tipoDoc switch
            {
                1 => "Invoices",
                5 => "CreditNotes",
                6 => "CreditNotes",
                7 => "DeliveryNotes",
                _ => throw new Exception($"Tipo de documento no soportado para actualizar evento de cancelación: {tipoDoc}")
            };

            var requestBody = new
            {
                U_EXX_FE_ANULACION_ESTADO = estadoInternoSAP,
                U_EXX_FE_ANULACION_RESP = descripcionRespuesta,
                U_EXX_FE_ANULACION_FECHA = fechaAutorizacion
            };

            var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
            response = await sapClient.PatchAsync($"{entidad}({docEntry})", content);
        }

        if (!response.IsSuccessStatusCode)
        {
            string errorContent = await response.Content.ReadAsStringAsync();
            _log.LogError($"Error al actualizar documento {docEntry} en SAP. Estado: {estadoSifen}, Código: {estadoInternoSAP}");
            _log.LogError($"Respuesta del Service Layer: {errorContent}");
        }

        return response.IsSuccessStatusCode;
    }
}