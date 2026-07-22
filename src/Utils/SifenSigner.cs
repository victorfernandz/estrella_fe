using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;

public class SifenSigner
{
//    public static void FirmarXml(XmlDocument xmlDoc, string cdc, string dRucReceptor, byte[] certificadoBytes, string contraseñaCertificado, int iNatRec)
    public static void FirmarXml(XmlDocument xmlDoc, string cdc, string dRucReceptor, byte[] certificadoBytes, string contraseñaCertificado, int iNatRec, SifenConfig sifen)
    {
        var cert = new X509Certificate2(certificadoBytes, contraseñaCertificado, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
        var privateKey = cert.GetRSAPrivateKey();
        if (privateKey == null) throw new Exception("Clave privada RSA no disponible en el certificado.");

        var deElement = xmlDoc.GetElementsByTagName("DE")[0] as XmlElement;
        if (deElement == null) throw new Exception("Elemento <DE> no encontrado en el XML.");

        // Establecer atributo Id 
        XmlAttribute idAttr = xmlDoc.CreateAttribute("Id");
        idAttr.Value = cdc;
        deElement.Attributes.Append(idAttr);

        if (deElement.HasAttribute("xmlns"))
            deElement.RemoveAttribute("xmlns");

        // Crear y configurar la firma
        SignedXml signedXml = new SignedXmlWithId(xmlDoc);
        signedXml.SigningKey = privateKey;

        Reference reference = new Reference("#" + cdc);
        reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        reference.AddTransform(new XmlDsigExcC14NTransform());
        signedXml.AddReference(reference);

        signedXml.SignedInfo.CanonicalizationMethod = "http://www.w3.org/2001/10/xml-exc-c14n#";
        signedXml.SignedInfo.SignatureMethod = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256";

        KeyInfo keyInfo = new KeyInfo();
        keyInfo.AddClause(new KeyInfoX509Data(cert));
        signedXml.KeyInfo = keyInfo;

        signedXml.ComputeSignature();
        XmlElement xmlDigitalSignature = signedXml.GetXml();

        // Insertar la firma
        xmlDoc.DocumentElement.AppendChild(xmlDoc.ImportNode(xmlDigitalSignature, true));

        // Obtener el DigestValue generado
        string digestValueReal = xmlDigitalSignature.GetElementsByTagName("DigestValue")[0]?.InnerText;
        //    InsertarQRCode(xmlDoc, cdc, dRucReceptor, digestValueReal, iNatRec);
        InsertarQRCode(xmlDoc, cdc, dRucReceptor, digestValueReal, iNatRec, sifen);

        Console.WriteLine("Firma aplicada correctamente con QR");
    }

    private static void InsertarQRCode(XmlDocument xmlDoc, string cdc, string dRucRec, string digestBase64, int iNatRec, SifenConfig sifen)
    {
        try
        {
            string fechaStr = xmlDoc.GetElementsByTagName("dFeEmiDE")[0].InnerText;
            byte[] fechaBytes = Encoding.UTF8.GetBytes(fechaStr);
            string fechaHex = BitConverter.ToString(fechaBytes).Replace("-", "").ToLower();

            string totalGral = xmlDoc.GetElementsByTagName("dTotGralOpe")[0].InnerText;
            string totalIVA = xmlDoc.GetElementsByTagName("dTotIVA")[0].InnerText;
            string cItems = xmlDoc.GetElementsByTagName("gCamItem").Count.ToString();

            byte[] digestAsciiBytes = Encoding.UTF8.GetBytes(digestBase64);
            string digestHex = BitConverter.ToString(digestAsciiBytes).Replace("-", "").ToLower();

            var config = Config.LoadConfig();
            string idCSC = sifen.IdCSC;
            string csc = sifen.CSC;
            string? cadenaQR = null;

            if (iNatRec == 1)
            {
                cadenaQR =
                    $"nVersion=150" +
                    $"&Id={cdc}" +
                    $"&dFeEmiDE={fechaHex}" +
                    $"&dRucRec={dRucRec}" +
                    $"&dTotGralOpe={totalGral}" +
                    $"&dTotIVA={totalIVA}" +
                    $"&cItems={cItems}" +
                    $"&DigestValue={digestHex}" +
                    $"&IdCSC={idCSC}";
            }
            else
            {
                cadenaQR =
                    $"nVersion=150" +
                    $"&Id={cdc}" +
                    $"&dFeEmiDE={fechaHex}" +
                    $"&dNumIDRec={dRucRec}" +
                    $"&dTotGralOpe={totalGral}" +
                    $"&dTotIVA={totalIVA}" +
                    $"&cItems={cItems}" +
                    $"&DigestValue={digestHex}" +
                    $"&IdCSC={idCSC}";
            }

            string cadenaParaHash = cadenaQR + csc;
            string cHashQR;

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(cadenaParaHash));
                cHashQR = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            }

            string? urlQR = null;

        //    if (config.Sifen.Url.ToLower().Contains("test"))
            if (sifen.Url.ToLower().Contains("test"))
            {
                urlQR = "https://ekuatia.set.gov.py/consultas-test/qr?" + cadenaQR + "&cHashQR=" + cHashQR;
            }
            else
            {
                urlQR = "https://ekuatia.set.gov.py/consultas/qr?" + cadenaQR + "&cHashQR=" + cHashQR;
            }

            XmlElement gCamFuFD = xmlDoc.CreateElement("gCamFuFD", xmlDoc.DocumentElement.NamespaceURI);
            XmlElement dCarQR = xmlDoc.CreateElement("dCarQR", xmlDoc.DocumentElement.NamespaceURI);
            dCarQR.InnerText = urlQR;
            gCamFuFD.AppendChild(dCarQR);

            var existentes = xmlDoc.GetElementsByTagName("gCamFuFD");
            if (existentes.Count > 0)
            {
                existentes[0].ParentNode.RemoveChild(existentes[0]);
            }

            xmlDoc.DocumentElement.AppendChild(gCamFuFD);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al generar código QR: {ex.Message}");
            throw;
        }
    }

    public class SignedXmlWithId : SignedXml
    {
        public SignedXmlWithId(XmlDocument doc) : base(doc) { }

        public override XmlElement GetIdElement(XmlDocument document, string idValue)
        {
            XmlElement idElem = base.GetIdElement(document, idValue);
            if (idElem != null) return idElem;

            XmlNodeList elems = document.GetElementsByTagName("*");
            foreach (XmlElement elem in elems)
            {
                if (elem.HasAttribute("Id") && elem.GetAttribute("Id") == idValue)
                    return elem;
            }

            return null;
        }
    }

    public static XmlDocument FirmarEvento(XmlDocument xmlDoc, string referenceId, byte[] certificadoBytes, string contraseñaCertificado, SifenConfig sifen)
    {
        var cert = new X509Certificate2(certificadoBytes, contraseñaCertificado, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
        var privateKey = cert.GetRSAPrivateKey();
        if (privateKey == null) throw new Exception("Clave privada RSA no disponible en el certificado.");

        // Buscar rEve ignorando namespaces
        XmlElement rEve = xmlDoc.SelectSingleNode("//*[local-name()='rEve']") as XmlElement;

        if (rEve == null)
            throw new Exception("No se encontró el nodo <rEve> para firmar.");

        // El atributo Id NO debe tener # como prefijo
        rEve.SetAttribute("Id", referenceId);

        SignedXml signedXml = new SignedXmlWithId(xmlDoc);
        signedXml.SigningKey = privateKey;

        // Añadir # a la referencia, pero no incluirlo en el atributo Id
        Reference reference = new Reference("#" + referenceId);
        reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        reference.AddTransform(new XmlDsigExcC14NTransform());
        reference.DigestMethod = SignedXml.XmlDsigSHA256Url;

        signedXml.AddReference(reference);

        signedXml.SignedInfo.CanonicalizationMethod = SignedXml.XmlDsigExcC14NTransformUrl;
        signedXml.SignedInfo.SignatureMethod = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256";

        KeyInfo keyInfo = new KeyInfo();
        KeyInfoX509Data x509Data = new KeyInfoX509Data(cert);
        
        keyInfo.AddClause(x509Data);
        signedXml.KeyInfo = keyInfo;

        signedXml.ComputeSignature();
        XmlElement xmlDigitalSignature = signedXml.GetXml();

        // Insertar la firma como hermano de rEve
        XmlNode rGesEve = rEve.ParentNode;
        if (rGesEve != null)
        {
            rGesEve.AppendChild(xmlDoc.ImportNode(xmlDigitalSignature, true));
        }

        return xmlDoc;
    }

    public static XmlDocument FirmarCancelacion(XmlDocument xmlDoc, string referenceId, byte[] certificadoBytes, string contraseñaCertificado, SifenConfig sifen)
    {
        var cert = new X509Certificate2(certificadoBytes, contraseñaCertificado,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);

        var privateKey = cert.GetRSAPrivateKey();
        if (privateKey == null) throw new Exception("Clave privada RSA no disponible en el certificado.");

        // Buscar rEve por Id si existe, si no tomar el único rEve
        XmlElement rEve =xmlDoc.SelectSingleNode($"//*[local-name()='rEve' and @Id='{referenceId}']") as XmlElement?? xmlDoc.SelectSingleNode("//*[local-name()='rEve']") as XmlElement;

        if (rEve == null)
            throw new Exception("No se encontró el nodo <rEve> para firmar.");

        // Asegurar Id
        if (rEve.GetAttribute("Id") != referenceId)
            rEve.SetAttribute("Id", referenceId);

        SignedXml signedXml = new SignedXmlWithId(xmlDoc);
        signedXml.SigningKey = privateKey;

        Reference reference = new Reference("#" + referenceId);
        reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        reference.AddTransform(new XmlDsigExcC14NTransform());
        reference.DigestMethod = SignedXml.XmlDsigSHA256Url;

        signedXml.AddReference(reference);

        signedXml.SignedInfo.CanonicalizationMethod = "http://www.w3.org/TR/2001/REC-xml-c14n-20010315";
        signedXml.SignedInfo.SignatureMethod = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256";

        KeyInfo keyInfo = new KeyInfo();
        keyInfo.AddClause(new KeyInfoX509Data(cert));
        signedXml.KeyInfo = keyInfo;

        signedXml.ComputeSignature();
        XmlElement xmlDigitalSignature = signedXml.GetXml();

        // Insertar la firma como hermano de rEve (en rGesEve)
        XmlNode rGesEve = rEve.ParentNode;
        if (rGesEve == null)
            throw new Exception("No se encontró el nodo padre <rGesEve> para insertar la firma.");

        rGesEve.AppendChild(xmlDoc.ImportNode(xmlDigitalSignature, true));

        // Limpieza: evitar xmlns="" que rompe validación
        var emptyNsNodes = xmlDoc.SelectNodes("//*[@xmlns='']");
        if (emptyNsNodes != null)
        {
            foreach (XmlNode n in emptyNsNodes)
            {
                if (n is XmlElement el) el.RemoveAttribute("xmlns");
            }
        }

        return xmlDoc;
    }
}