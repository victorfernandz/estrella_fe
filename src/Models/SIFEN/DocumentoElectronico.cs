using System;
using System.Xml.Serialization;
using System.Xml;
using System.Globalization;

[XmlRoot("rDE", Namespace = "http://ekuatia.set.gov.py/sifen/xsd")]

// Campos que identifican el formato electrónico XML (AA001-AA009)
public class DocumentoElectronico // Nodo Padre AA001
{
    public DocumentoElectronico()
    {
        Xmlns = new XmlSerializerNamespaces();
        Xmlns.Add("", "http://ekuatia.set.gov.py/sifen/xsd");
        Xmlns.Add("xsi", "http://www.w3.org/2001/XMLSchema-instance");
    }

    [XmlNamespaceDeclarations]
    public XmlSerializerNamespaces Xmlns;

    [XmlElement("dVerFor")]
    public string VersionFormato { get; set; } = "150";

    [XmlElement("DE")]
    public DEContent DE { get; set; }
/*
    [XmlElement("gCamFuFD")]
    public gCamFuFD gCamFuFD { get; set; }
*/
    public DocumentoElectronico(string cdc, int dv, DateTime dFecFirma, int dSisFact, string dCodSeg, string iTiDE, int dNumTim, string dEst, string dPunExp, string dNumDoc, DateTime dFeIniT, DateTime dFeEmiDE, string? iTipTra, string cMoneOpe,
        string dDesMoneOpe, string dRucEm, int dDVEmi, int iTipCont, string dNomEmi, string dDirEmi, int dNumCas, int cDepEmi, string dDesDepEmi, int cDisEmi, string dDesDisEmi, int cCiuEmi, string dDesCiuEmi, string dTelEmi, 
        string dEmailE, string cActEco, string dDesActEco, int iNatRec, int? iTiContRec, string? dDirRec, int? dNumCasRec, int iTiOpe, string cPaisRec, string dDesPaisRe, string dNomRec, string dRucRec, int? dDVRec,
        string? dTelRec, string? dCelRec, string? dEmailRec,
        decimal dTiCam, int? iIndPres, int? iCondOpe, int? iCondCred, int? iTiPago, decimal? dMonTiPag,
        string? cMoneTiPag, string? dDMoneTiPag, decimal? dTiCamTiPag, string? iTipIDRec, string? dNumIDRec,

        // campos opcionales solo para NC
        int? iMotEmi = null, string? dCdCDERef = null, DateTime? dFecEmiDI = null, int? dNTimDI = null, string? dEstDocAso = null, string? dPExpDocAso = null, string? dNumDocAso = null, int? iTipoDocAso = null, int? iTipDocAso = null,
        // campos opcionales solo para NRE
        GCamNRE? camposNRE = null, GTransp? transporte = null)

    {
        DE = new DEContent(cdc, dv, dFecFirma, dSisFact, dCodSeg, iTiDE, dNumTim, dEst, dPunExp, dNumDoc, dFeIniT, dFeEmiDE, iTipTra, cMoneOpe, dDesMoneOpe, dRucEm, dDVEmi, iTipCont, dNomEmi, dDirEmi, dNumCas, cDepEmi, dDesDepEmi, cDisEmi, dDesDisEmi,
            cCiuEmi, dDesCiuEmi, dTelEmi, dEmailE, cActEco, dDesActEco, iNatRec, iTiContRec, dDirRec, dNumCasRec, iTiOpe, cPaisRec, dDesPaisRe, dNomRec, dRucRec, dDVRec, dTelRec, dCelRec, dEmailRec,
            dTiCam, iIndPres, iCondOpe, iCondCred, iTiPago, dMonTiPag, cMoneTiPag, dDMoneTiPag, dTiCamTiPag, iTipIDRec, dNumIDRec,
            iMotEmi, dCdCDERef, dFecEmiDI, dNTimDI, dEstDocAso, dPExpDocAso, dNumDocAso, iTipoDocAso, iTipDocAso, camposNRE, transporte);
    }
}

// Campos firmados del Documento Electrónico (A001-A099)
public class DEContent // Nodo padre AA001
{
    [XmlAttribute("Id")]
    public string Id { get; set; } // El Id es el CDC según el manual técnico

    [XmlElement("dDVId")]
    public int DigitoVerificador { get; set; }

    [XmlIgnore]
    public DateTime FechaFirma { get; set; }

    [XmlElement("dFecFirma")]
    public string FechaFirmaString
    {
        get => FechaFirma.ToString("yyyy-MM-ddTHH:mm:ss"); 
        set => FechaFirma = DateTime.ParseExact(value, "yyyy-MM-ddTHH:mm:ss", null);
    }

    [XmlElement("dSisFact")]
    public int SistemaFacturacion { get; set; } 

    [XmlElement("gOpeDE")]
    public GOpeDE GrupoOperacion { get; set; }

    [XmlElement("gTimb")]
    public GTimb GrupoTimbrado { get; set; }

    [XmlElement("gDatGralOpe")]
    public GDatGralOpe CamposGenerales { get; set; }

    [XmlElement("gDtipDE")]
    public GDtipDE CamposEspecificosTipoDocumento { get; set; }

    [XmlElement("gTotSub")]
    public GTotSub CamposTotalesSubtotales { get; set; }

    [XmlElement("gCamDEAsoc")]
    public GCamDEAsoc DocumentoAsociado { get; set; }

    public bool ShouldSerializeDocumentoAsociado()
    {
        return DocumentoAsociado != null;
    }

    public DEContent() {}

    public DEContent(string cdc, int dv, DateTime dFecFirma, int dSisFact, string dCodSeg, string iTiDE, int dNumTim, string dEst, string dPunExp, string dNumDoc, DateTime dFeIniT, DateTime dFeEmiDE, string? iTipTra, string cMoneOpe, string dDesMoneOpe,
        string dRucEm,int dDVEmi, int iTipCont, string dNomEmi, string dDirEmi, int dNumCas, int cDepEmi, string dDesDepEmi, int cDisEmi, string dDesDisEmi, int cCiuEmi, string dDesCiuEmi, string dTelEmi, string dEmailE,
        string cActEco, string dDesActEco, int iNatRec, int? iTiContRec, string? dDirRec, int? dNumCasRec, int iTiOpe, string cPaisRec, string dDesPaisRe, string dNomRec, string dRucRec, int? dDVRec,
        string? dTelRec, string? dCelRec, string? dEmailRec,
        decimal dTiCam, int? iIndPres, int? iCondOpe, int? iCondCred, int? iTiPago, decimal? dMonTiPag,
        string? cMoneTiPag, string? dDMoneTiPag, decimal? dTiCamTiPag, string? iTipIDRec, string? dNumIDRec,
        // campos opcionales solo para NC
        int? iMotEmi = null, string? dCdCDERef = null, DateTime? dFecEmiDI = null, int? dNTimDI = null, string? dEstDocAso = null, string? dPExpDocAso = null, string? dNumDocAso = null, int? iTipoDocAso = null, int? iTipDocAso = null,
        // campos opcionales solo para NRE
        GCamNRE? camposNRE = null, GTransp? transporte = null)

    {
        Id = cdc;
        DigitoVerificador = dv;
        FechaFirma = dFecFirma;
        SistemaFacturacion = dSisFact;
        GrupoOperacion = new GOpeDE(dCodSeg);
        GrupoTimbrado = new GTimb(iTiDE, dNumTim, dEst, dPunExp, dNumDoc, dFeIniT);
            
        CamposGenerales = new GDatGralOpe(dFeEmiDE, iTipTra, cMoneOpe, dDesMoneOpe, dRucEm, dDVEmi, iTipCont, dNomEmi, dDirEmi, dNumCas, cDepEmi, dDesDepEmi, cDisEmi, dDesDisEmi, cCiuEmi, dDesCiuEmi, dTelEmi, dEmailE,
            iNatRec, iTiContRec, dDirRec, dNumCasRec, iTiOpe, cPaisRec, dDesPaisRe, dNomRec, dRucRec, dDVRec, dTelRec, dCelRec, dEmailRec, dTiCam, iTipIDRec, dNumIDRec);
            
        // Agregar la actividad económica si se proporcionó
        if (!string.IsNullOrEmpty(cActEco))
        {
            CamposGenerales.GrupoCamposEmisor.ActividadesEconomicas.Add(new GActEco(cActEco, dDesActEco));
        }

        CamposEspecificosTipoDocumento = new GDtipDE();

        if(iTiDE == "5")
        {
            CamposEspecificosTipoDocumento.CamposNotaCreditoElectronica = new GCamNCDE(iMotEmi);
            DocumentoAsociado = new GCamDEAsoc(iTipDocAso, dCdCDERef, dNTimDI, dEstDocAso, dPExpDocAso, dNumDocAso, iTipoDocAso, dFecEmiDI);
        }
        else if (iTiDE == "7")
        {
            CamposEspecificosTipoDocumento.CamposNotaRemisionElectronica = camposNRE;
            CamposEspecificosTipoDocumento.GrupoTransporte = transporte;

            // El documento asociado es opcional en la NRE y, si se informa, solo puede referenciar una Factura Electrónica
            if (iTipDocAso.HasValue)
            {
                DocumentoAsociado = new GCamDEAsoc(iTipDocAso, dCdCDERef, dNTimDI, dEstDocAso, dPExpDocAso, dNumDocAso, iTipoDocAso, dFecEmiDI);
            }
        }
        else
        {
            CamposEspecificosTipoDocumento = new GDtipDE(iIndPres, iCondOpe, iCondCred, iTiPago, dMonTiPag, cMoneTiPag, dDMoneTiPag, dTiCamTiPag);
        }
    }
}

// Campos inherentes a la operación de Documentos Electrónicos (B001-B099)
public class GOpeDE // Nodo padre A001
{
    [XmlElement("iTipEmi")]
    public int TipoEmision { get; set; } = 1;

    [XmlElement("dDesTipEmi")]
    public string DescripcionTipoEmision { get; set; } = "Normal";

    [XmlElement("dCodSeg")]
    public string CodigoSeguridad { get; set; }

    public GOpeDE() {}

    public GOpeDE(string dCodSeg)
    {
        CodigoSeguridad = dCodSeg;
    }
}

// Campos de datos del Timbrado (C001-C099)
public class GTimb //  Nodo padre A001
{
    [XmlElement("iTiDE")]
    public string TipoDocumento { get; set; }

    [XmlElement("dDesTiDE")]
    public string DescripcionTipoDocumento { get; set; }

    [XmlElement("dNumTim")]
    public int NumeroTimbrado { get; set; }

    [XmlElement("dEst")]
    public string Establecimiento { get; set; }

    [XmlElement("dPunExp")]
    public string PuntoDeExpedicion { get; set; }

    [XmlElement("dNumDoc")]
    public string NumeroDocumento { get; set; }

    [XmlIgnore] // Evita serializar el DateTime directamente
    public DateTime FechaInicioTimbrado { get; set; }

    [XmlElement("dFeIniT")]
    public string FechaInicioTimbradoString
    {
        get => FechaInicioTimbrado.ToString("yyyy-MM-dd"); 
        set => FechaInicioTimbrado = DateTime.ParseExact(value, "yyyy-MM-dd", null);
    }

    public GTimb() {}

    public GTimb(string iTiDE, int dNumTim, string dEst, string dPunExp, string dNumDoc, DateTime dFeIniT)
    {
        TipoDocumento = iTiDE;
        DescripcionTipoDocumento = ObtenerDescripcionTipoDocumento(iTiDE);
        NumeroTimbrado = dNumTim;
        Establecimiento = dEst;
        PuntoDeExpedicion = dPunExp;
        NumeroDocumento = dNumDoc;
        FechaInicioTimbrado = dFeIniT;
    }

    private string ObtenerDescripcionTipoDocumento(string iTiDE)
    {
        return iTiDE switch
        {
            "1" => "Factura electrónica",
            "2" => "Factura electrónica de exportación",
            "3" => "Factura electrónica de importación",
            "4" => "Autofactura electrónica",
            "5" => "Nota de crédito electrónica",
            "6" => "Nota de débito electrónica",
            "7" => "Nota de remisión electrónica",
            "8" => "Comprobante de retención electrónica"
        };
    }
}