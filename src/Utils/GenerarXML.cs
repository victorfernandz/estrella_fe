using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using System.Globalization;

public class GenerarXML
{
    public static void SerializarDocumentoElectronico(SifenConfig sifen, string cdc, int dv, DateTime dFecFirma, string rutaArchivo, string dCodSeg, string iTiDE, int dNumTim, string dEst, string dPunExp, string dNumDoc, DateTime dFeIniT, DateTime dFeEmiDE,
        string? iTipTra, string cMoneOpe, string dDesMoneOpe, string dRucEm, int dDVEmi, int iTipCont, string dNomEmi, string dDirEmi, int dNumCas, int cDepEmi, string dDesDepEmi, int cDisEmi, string dDesDisEmi, int cCiuEmi, string dDesCiuEmi, string dTelEmi,
        string dEmailE, int iNatRec, int iTiContRec, string dDirRec, int? dNumCasRec, int iTiOpe, string cPaisRec, string dDesPaisRe, string dNomRec, string dRucReceptor, int? dDVReceptor,
        string? dTelRec, string? dCelRec, string? dEmailRec,
        decimal dTiCam, int? iIndPres, int? iCondOpe, int? iCondCred, int? iTiPago, decimal? dMonTiPag,
        string? cMoneTiPag, string? dDMoneTiPag, decimal? dTiCamTiPag, string? iTipIDRec, string? dNumIDRec,
        List<ActividadEconomica> actividades, List<ObligacionAfectada>? obligaciones = null, List<GCuotas>? cuotas = null, List<Item> items = null, string plazoCredito = null, GTotSub totales = null,
        byte[]? certificadoBytes = null, string? contraseñaCertificado = null,
        // campos opcionales solo para NC
        int? iMotEmi = null, string? dCdCDERef = null, DateTime? dFecEmiDI = null, int? dNTimDI = null, string? dEstDocAso = null, string? dPExpDocAso = null, string? dNumDocAso = null, int? iTipDocAso = null, int? iTipoDocAso = null,
        // campos opcionales solo para NRE
        GCamNRE? camposNRE = null, GTransp? transporte = null)

    {
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;

            var actividadPrincipal = actividades.First();

            DocumentoElectronico documento = new DocumentoElectronico(cdc, dv, dFecFirma, 1, dCodSeg, iTiDE, dNumTim, dEst, dPunExp, dNumDoc, dFeIniT, dFeEmiDE, iTipTra, cMoneOpe, dDesMoneOpe, dRucEm, dDVEmi, iTipCont, dNomEmi, dDirEmi,
                dNumCas, cDepEmi, dDesDepEmi, cDisEmi, dDesDisEmi, cCiuEmi, dDesCiuEmi, dTelEmi, dEmailE, actividadPrincipal.Codigo, actividadPrincipal.Descripcion, iNatRec, iTiContRec, dDirRec, dNumCasRec, iTiOpe, cPaisRec, dDesPaisRe, dNomRec,
                dRucReceptor, dDVReceptor, dTelRec, dCelRec, dEmailRec, dTiCam, iIndPres, iCondOpe, iCondCred, iTiPago, dMonTiPag, cMoneTiPag, dDMoneTiPag, dTiCamTiPag, iTipIDRec, dNumIDRec,

            // Campos adicionales solo para Nota de Crédito / Nota de Remisión (documento asociado)
            iTiDE == "5" ? iMotEmi : null,
            (iTiDE == "5" || iTiDE == "7") ? dCdCDERef : null,
            (iTiDE == "5" || iTiDE == "7") ? dFecEmiDI : null,
            (iTiDE == "5" || iTiDE == "7") ? dNTimDI : null,
            (iTiDE == "5" || iTiDE == "7") ? dEstDocAso : null,
            (iTiDE == "5" || iTiDE == "7") ? dPExpDocAso : null,
            (iTiDE == "5" || iTiDE == "7") ? dNumDocAso : null,
            (iTiDE == "5" || iTiDE == "7") ? iTipoDocAso : null,
            (iTiDE == "5" || iTiDE == "7") ? iTipDocAso : null,

            // Campos adicionales solo para Nota de Remisión
            iTiDE == "7" ? camposNRE : null,
            iTiDE == "7" ? transporte : null
        );

            // Agregar actividades económicas adicionales
            if (actividades.Count > 1)
            {
                for (int i = 1; i < actividades.Count; i++)
                {
                    documento.DE.CamposGenerales.GrupoCamposEmisor.ActividadesEconomicas.Add(new GActEco(actividades[i].Codigo, actividades[i].Descripcion));
                }
            }

            // Agregar obligaciones
            if (obligaciones != null && obligaciones.Any())
            {
                foreach (var obligacion in obligaciones)
                {
                    documento.DE.CamposGenerales.OperacionComercial.ObligacionesAfectadas.Add(new GOblAfe(obligacion.Codigo, obligacion.Descripcion));
                }
            }

            // Configurar condiciones de operación y crédito
            if (iCondOpe == 2)
            {
                if (iCondCred == 1)
                {
                    string plazoFinal = string.IsNullOrEmpty(plazoCredito) ?
                        (cuotas?.FirstOrDefault()?.FechaVencimientoCuota ?? "30 días") :
                        plazoCredito;

                    documento.DE.CamposEspecificosTipoDocumento.CondicionOperacion.OperacionCredito =
                        new GPagCred(1, plazoFinal, null);
                }
                else if (iCondCred == 2)
                {
                    int cantidadCuotas = cuotas?.Count ?? 0;
                    documento.DE.CamposEspecificosTipoDocumento.CondicionOperacion.OperacionCredito =
                        new GPagCred(2, null, cantidadCuotas);

                    if (cuotas != null && cuotas.Any())
                    {
                        foreach (var cuota in cuotas)
                        {
                            documento.DE.CamposEspecificosTipoDocumento.CondicionOperacion.OperacionCredito.Cuotas.Add(cuota);
                        }
                    }
                }
            }

            // Agregar items
            bool esNotaRemision = iTiDE == "7";

            if (items != null && items.Any())
            {
                documento.DE.CamposEspecificosTipoDocumento.Items.Clear();
                foreach (var item in items)
                {
                    // La Nota de Remisión Electrónica no informa precio ni IVA por ítem (reglas E720a/E730a)
                    GValorItem valorItem = null;
                    GCamIVA camposIVA = null;

                    if (!esNotaRemision)
                    {
                        int condicionTipoCambio = documento.DE.CamposGenerales.OperacionComercial.CondicionTipoCambio;
                        valorItem = new GValorItem
                        {
                            PrecioUnitario = item.dPUniProSer,
                            TipoCambioIt = item.dTiCamIt,
                            TotalBrutoItem = item.dTotBruOpeItem,
                            ValorRestaItem = new GValorRestaItem { TotalOperacionItem = item.dTotBruOpeItem },
                            MonedaOperacion = cMoneOpe,
                            EsTipoCambioGlobal = condicionTipoCambio == 1
                        };

                        camposIVA = new GCamIVA
                        {
                            AfectacionIVA = item.iAfecIVA,
                            DescripcionAfectacionIVA = item.dDesAfecIVA,
                            ProporcionIVA = item.dPropIVA,
                            TasaIVA = (int)item.dTasaIVA,
                            BaseGravadaIVA = item.dBasGravIVA,
                            LiquidacionIVA = item.dLiqIVAItem,
                            BaseExenta = item.dBasExe
                        };
                    }

                    documento.DE.CamposEspecificosTipoDocumento.Items.Add(new GCamItem
                    {
                        CodigoItem = item.dCodInt,
                        DescripcionItem = item.dDesProSer,
                        UnidadMedida = item.cUniMed > 0 ? item.cUniMed : 77,
                        DescripcionUnidadMedida = !string.IsNullOrWhiteSpace(item.dDesUniMed) ? item.dDesUniMed : "UNI",
                        CantidadProducto = item.dCantProSer,
                        CodigoRelevanciaMercaderia = item.cRelMerc,
                        DescRelevanciaMercaderia = item.cRelMerc switch
                        {
                            1 => "Tolerancia de quiebra",
                            2 => "Tolerancia de merma",
                            _ => null
                        },
                        CantidadQuiebraMerma = item.dCanQuiMer,
                        PorcentajeQuiebraMerma = item.dPorQuiMer,
                        EsNotaRemision = esNotaRemision,
                        ValorItem = valorItem,
                        CamposIVA = camposIVA
                    });
                }
            }

            // Calcular totales (la Nota de Remisión Electrónica no informa el grupo de totales, regla F001a del Manual Técnico)
            documento.DE.CamposTotalesSubtotales = esNotaRemision
                ? null
                : totales ?? (items != null && items.Any() ? Totalizador.CalcularTotalesFactura(items, dTiCam, cMoneOpe) : new GTotSub());

            // Serializar usando MemoryStream
            var serializer = new XmlSerializer(typeof(DocumentoElectronico));
            var xmlDoc = new XmlDocument { PreserveWhitespace = true };

            using (var ms = new MemoryStream())
            using (var writer = XmlWriter.Create(ms, new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false),
                Indent = false,
                OmitXmlDeclaration = true,
                NewLineHandling = NewLineHandling.None
            }))
            {
                serializer.Serialize(writer, documento);
                writer.Flush();

                // Validar XML generado como string antes de cargar al XmlDocument
                string xmlStringGenerado = Encoding.UTF8.GetString(ms.ToArray());
                ms.Position = 0;
                xmlDoc.Load(ms);
            }

            // Preparar namespace y atributos de schema
            var root = xmlDoc.DocumentElement;
            root.Attributes.RemoveNamedItem("xmlns");
            root.Attributes.RemoveNamedItem("xmlns:xsi");
            root.Attributes.RemoveNamedItem("xmlns:xsd");
            root.Attributes.RemoveNamedItem("xsi:schemaLocation");

            root.SetAttribute("xmlns", "http://ekuatia.set.gov.py/sifen/xsd");

            XmlAttribute xmlnsXsi = xmlDoc.CreateAttribute("xmlns", "xsi", "http://www.w3.org/2000/xmlns/");
            xmlnsXsi.Value = "http://www.w3.org/2001/XMLSchema-instance";
            root.Attributes.Append(xmlnsXsi);

            XmlAttribute schemaLocation = xmlDoc.CreateAttribute("xsi", "schemaLocation", "http://www.w3.org/2001/XMLSchema-instance");
            schemaLocation.Value = "http://ekuatia.set.gov.py/sifen/xsd siRecepDE_v150.xsd";
            root.Attributes.Append(schemaLocation);

            // Firmar el XML
            if (certificadoBytes != null && !string.IsNullOrEmpty(contraseñaCertificado))
            {
                var config = Config.LoadConfig();
                //    var sifenConfig = config.SapServiceLayerList.First().Sifen;
                string rutaDebug = Path.Combine(Path.GetDirectoryName(rutaArchivo), "debug_pre_firma.xml");
                using (var fs = new FileStream(rutaDebug, FileMode.Create, FileAccess.Write))
                using (var writer = XmlWriter.Create(fs, new XmlWriterSettings
                {
                    Encoding = new UTF8Encoding(false),
                    Indent = false,
                    OmitXmlDeclaration = true,
                    NewLineHandling = NewLineHandling.None
                }))
                {
                    xmlDoc.Save(writer);
                    writer.Flush();
                }
                Console.WriteLine("XML guardado antes de firmar: " + rutaDebug);

                if (iNatRec == 1)
                {
                    //SifenSigner.FirmarXml(xmlDoc, cdc, dRucReceptor, certificadoBytes, contraseñaCertificado, iNatRec);
                    SifenSigner.FirmarXml(xmlDoc, cdc, dRucReceptor, certificadoBytes, contraseñaCertificado, iNatRec, sifen);

                }
                else
                {
                    SifenSigner.FirmarXml(xmlDoc, cdc, dNumIDRec, certificadoBytes, contraseñaCertificado, iNatRec, sifen);
                }
            }

            XmlWriterSettings settings = new XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                Encoding = new UTF8Encoding(false),
                Indent = false,
                NewLineHandling = NewLineHandling.None,
                CheckCharacters = false
            };

            using (XmlWriter writer = XmlWriter.Create(rutaArchivo, settings))
            {
                xmlDoc.Save(writer);
            }

            Console.WriteLine($"XML generado exitosamente: {rutaArchivo}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error en GenerarXML.SerializarDocumentoElectronico: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Error interno: {ex.InnerException.Message}");
            }
            throw new Exception($"Error al generar el XML: {ex.Message}", ex);
        }
    }
    
    public static void SerializarDocumentoInutilizacion(SifenConfig sifen, string cdc, DateTime dFecFirma, string rutaArchivo, int iTiDE, int dNumTim, string dEst, string dPunExp, string dNumIn, string dNumFin, string mOtEve, byte[]? certificadoBytes = null, string? contraseñaCertificado = null)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;

        DocumentoEvento evento = new DocumentoEvento(cdc, dFecFirma, dNumTim, dEst, dPunExp, dNumIn, dNumFin, iTiDE, mOtEve);

        // Serializar usando MemoryStream
        var serializer = new XmlSerializer(typeof(DocumentoEvento));
        var xmlDoc = new XmlDocument { PreserveWhitespace = true };

        using (var ms = new MemoryStream())
        using (var writer = XmlWriter.Create(ms, new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = false,
            OmitXmlDeclaration = true,
            NewLineHandling = NewLineHandling.None
        }))
        {
            serializer.Serialize(writer, evento);
            writer.Flush();

            // Validar XML generado como string antes de cargar al XmlDocument
            string xmlStringGenerado = Encoding.UTF8.GetString(ms.ToArray());
            xmlDoc.LoadXml(xmlStringGenerado);
        }

        // Preparar namespace y atributos de schema
        var root = xmlDoc.DocumentElement;
        if (root != null)
        {
            XmlAttribute xmlnsXsi = xmlDoc.CreateAttribute("xmlns", "xsi", "http://www.w3.org/2000/xmlns/");
            xmlnsXsi.Value = "http://www.w3.org/2001/XMLSchema-instance";
            root.Attributes.Append(xmlnsXsi);
        }
        
        // gGroupGesEve
        var gGroup = xmlDoc.SelectSingleNode("//*[local-name()='gGroupGesEve']") as XmlElement;
        if (gGroup != null)
        {
            // Crear elemento gGroupGesEve con el namespace SIFEN
            var nsSifen = "http://ekuatia.set.gov.py/sifen/xsd";
            XmlElement newGGroup = xmlDoc.CreateElement(gGroup.LocalName, nsSifen);

            // Mover todos los hijos
            while (gGroup.HasChildNodes)
            {
                XmlNode child = gGroup.FirstChild;
                gGroup.RemoveChild(child);
                newGGroup.AppendChild(child);
            }

            // xmlns:xsi
            XmlAttribute xmlnsXsi = xmlDoc.CreateAttribute("xmlns", "xsi", "http://www.w3.org/2000/xmlns/");
            xmlnsXsi.Value = "http://www.w3.org/2001/XMLSchema-instance";
            newGGroup.Attributes.Append(xmlnsXsi);

            // xsi:schemaLocation
            XmlAttribute schemaLocation = xmlDoc.CreateAttribute("xsi", "schemaLocation", "http://www.w3.org/2001/XMLSchema-instance");
            schemaLocation.Value = "http://ekuatia.set.gov.py/sifen/xsd siRecepEvento_v150.xsd";
            newGGroup.Attributes.Append(schemaLocation);

            // Reemplazar el nodo viejo por el nuevo en el árbol
            XmlNode parent = gGroup.ParentNode;
            parent.ReplaceChild(newGGroup, gGroup);
        }

        // Ahora agregar xsi:schemaLocation también en rGesEve
        var nsXsi = "http://www.w3.org/2001/XMLSchema-instance";
        var rGesEve = xmlDoc.SelectSingleNode("//*[local-name()='rGesEve']") as XmlElement;

        if (rGesEve != null)
        {
            XmlAttribute schemaLocation = xmlDoc.CreateAttribute("xsi", "schemaLocation", nsXsi);
            schemaLocation.Value = "http://ekuatia.set.gov.py/sifen/xsd siRecepEvento_v150.xsd";
            rGesEve.Attributes.Append(schemaLocation);
        }

        // Firmar el XML
        if (certificadoBytes != null && !string.IsNullOrEmpty(contraseñaCertificado))
        {
            string rutaDebug = Path.Combine(Path.GetDirectoryName(rutaArchivo), "debug_pre_firma.xml");
            using (var fs = new FileStream(rutaDebug, FileMode.Create, FileAccess.Write))
            using (var writer = XmlWriter.Create(fs, new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false),
                Indent = false,
                OmitXmlDeclaration = true,
                NewLineHandling = NewLineHandling.None
            }))
            {
                xmlDoc.Save(writer);
                writer.Flush();
            }
            Console.WriteLine("XML guardado antes de firmar: " + rutaDebug);
        }

        SifenSigner.FirmarEvento(xmlDoc, cdc, certificadoBytes, contraseñaCertificado, sifen);

        XmlWriterSettings settings = new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            Encoding = new UTF8Encoding(false),
            Indent = false,
            NewLineHandling = NewLineHandling.None,
            CheckCharacters = false
        };

        using (XmlWriter writer = XmlWriter.Create(rutaArchivo, settings))
        {
            xmlDoc.Save(writer);
        }
        Console.WriteLine($"XML generado exitosamente: {rutaArchivo}");
    }

    public static void SerializarDocumentoCancelacion(SifenConfig sifen, string idEvento, string cdc, DateTime dFecFirma, string rutaArchivo, string mOtEve, byte[]? certificadoBytes = null, string? contraseñaCertificado = null)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;

        // Construir XML base
        var nsSifen = "http://ekuatia.set.gov.py/sifen/xsd";
        var nsXsi = "http://www.w3.org/2001/XMLSchema-instance";

        var xmlDoc = new XmlDocument { PreserveWhitespace = true };

        // Root: <rEnviEventoDe xmlns="http://ekuatia.set.gov.py/sifen/xsd" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
        XmlElement root = xmlDoc.CreateElement("rEnviEventoDe", nsSifen);
        xmlDoc.AppendChild(root);

        XmlAttribute xmlnsXsi = xmlDoc.CreateAttribute("xmlns", "xsi", "http://www.w3.org/2000/xmlns/");
        xmlnsXsi.Value = nsXsi;
        root.Attributes.Append(xmlnsXsi);

        // <dId>...</dId>
        XmlElement dId = xmlDoc.CreateElement("dId", nsSifen);
        dId.InnerText = idEvento;
        root.AppendChild(dId);

        // <dEvReg>
        XmlElement dEvReg = xmlDoc.CreateElement("dEvReg", nsSifen);
        root.AppendChild(dEvReg);

        // <gGroupGesEve>
        XmlElement gGroupGesEve = xmlDoc.CreateElement("gGroupGesEve", nsSifen);

        // xmlns:xsi + xsi:schemaLocation en gGroupGesEve
        XmlAttribute xmlnsXsi2 = xmlDoc.CreateAttribute("xmlns", "xsi", "http://www.w3.org/2000/xmlns/");
        xmlnsXsi2.Value = nsXsi;
        gGroupGesEve.Attributes.Append(xmlnsXsi2);

        XmlAttribute schemaLocation = xmlDoc.CreateAttribute("xsi", "schemaLocation", nsXsi);
        schemaLocation.Value = "http://ekuatia.set.gov.py/sifen/xsd siRecepEvento_v150.xsd";
        gGroupGesEve.Attributes.Append(schemaLocation);

        dEvReg.AppendChild(gGroupGesEve);

        // <rGesEve>
        XmlElement rGesEve = xmlDoc.CreateElement("rGesEve", nsSifen);

        XmlAttribute schemaLocation2 = xmlDoc.CreateAttribute("xsi", "schemaLocation", nsXsi);
        schemaLocation2.Value = "http://ekuatia.set.gov.py/sifen/xsd siRecepEvento_v150.xsd";
        rGesEve.Attributes.Append(schemaLocation2);

        gGroupGesEve.AppendChild(rGesEve);

        // <rEve Id="...">
        XmlElement rEve = xmlDoc.CreateElement("rEve", nsSifen);
        XmlAttribute idAttr = xmlDoc.CreateAttribute("Id");
        idAttr.Value = idEvento;          
        rEve.Attributes.Append(idAttr);
        rGesEve.AppendChild(rEve);

        // <dFecFirma>
        XmlElement dFecFirmaEl = xmlDoc.CreateElement("dFecFirma", nsSifen);
        dFecFirmaEl.InnerText = dFecFirma.ToString("yyyy-MM-ddTHH:mm:ss");
        rEve.AppendChild(dFecFirmaEl);

        // <dVerFor>150</dVerFor>
        XmlElement dVerFor = xmlDoc.CreateElement("dVerFor", nsSifen);
        dVerFor.InnerText = "150";
        rEve.AppendChild(dVerFor);

        // <gGroupTiEvt>
        XmlElement gGroupTiEvt = xmlDoc.CreateElement("gGroupTiEvt", nsSifen);
        rEve.AppendChild(gGroupTiEvt);

        // <rGeVeCan>
        XmlElement rGeVeCan = xmlDoc.CreateElement("rGeVeCan", nsSifen);
        gGroupTiEvt.AppendChild(rGeVeCan);

        // <Id></Id> 
        XmlElement Id = xmlDoc.CreateElement("Id", nsSifen);
        Id.InnerText = cdc;
        rGeVeCan.AppendChild(Id);

        // <mOtEve>...</mOtEve>
        XmlElement mOtEveEl = xmlDoc.CreateElement("mOtEve", nsSifen);
        mOtEveEl.InnerText = mOtEve;
        rGeVeCan.AppendChild(mOtEveEl);

        // Debug pre-firma
        if (certificadoBytes != null && !string.IsNullOrEmpty(contraseñaCertificado))
        {
            string rutaDebug = Path.Combine(Path.GetDirectoryName(rutaArchivo), "debug_pre_firma_cancelacion.xml");
            using (var fs = new FileStream(rutaDebug, FileMode.Create, FileAccess.Write))
            using (var writer = XmlWriter.Create(fs, new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false),
                Indent = false,
                OmitXmlDeclaration = true,
                NewLineHandling = NewLineHandling.None
            }))
            {
                xmlDoc.Save(writer);
                writer.Flush();
            }
            Console.WriteLine("XML guardado antes de firmar (cancelación): " + rutaDebug);
        }

        SifenSigner.FirmarCancelacion(xmlDoc, idEvento, certificadoBytes, contraseñaCertificado, sifen);

        XmlWriterSettings settings = new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            Encoding = new UTF8Encoding(false),
            Indent = false,
            NewLineHandling = NewLineHandling.None,
            CheckCharacters = false
        };

        using (XmlWriter writer = XmlWriter.Create(rutaArchivo, settings))
        {
            xmlDoc.Save(writer);
        }

        Console.WriteLine($"XML de cancelación generado exitosamente: {rutaArchivo}");
    }
}