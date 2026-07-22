using System;
using System.Xml.Serialization;
using System.Xml;

//[XmlRoot("rEnviEventoDe")]
[XmlRoot("rEnviEventoDe", Namespace = "http://ekuatia.set.gov.py/sifen/xsd")]

public class DocumentoEvento
{
    public DocumentoEvento()
    {
        Xmlns = new XmlSerializerNamespaces();
        Xmlns.Add("", "http://ekuatia.set.gov.py/sifen/xsd");
        Xmlns.Add("xsi", "http://www.w3.org/2001/XMLSchema-instance");
    }

    [XmlNamespaceDeclarations]
    public XmlSerializerNamespaces Xmlns;

    [XmlElement("dId")]
    public string dId { get; set; }

    [XmlElement("dEvReg")]
    public DEvReg dEvReg { get; set; }

    [XmlElement("rEve")]
    public EventoDE rEve { get; set; }

    public DocumentoEvento(string cdc, DateTime dFecFirma, int dNumTim, string dEst, string dPunExp, string dNumIn, string dNumFin, int iTiDE, string mOtEve)
    {
        dId = cdc;
        dEvReg = new DEvReg
        {
            GrupoEventos = new GGroupGesEve
            {
                GestionEvento = new RGesEve
                {
                    rEve = new EventoDE(cdc, dFecFirma, dNumTim, dEst, dPunExp, dNumIn, dNumFin, iTiDE, mOtEve)
                }
            }
        };
    }

    public class DEvReg
    {
        [XmlElement("gGroupGesEve")]
        public GGroupGesEve GrupoEventos { get; set; }
    }

    public class GGroupGesEve
    {        
        [XmlElement("rGesEve")]
        public RGesEve GestionEvento { get; set; }
    }

    public class RGesEve
    {
        [XmlElement("rEve")]
        public EventoDE rEve { get; set; }
    }

    public class EventoDE
    {
        [XmlAttribute("Id")]
        public string Id { get; set; }

        [XmlIgnore]
        public DateTime FechaFirma { get; set; }

        [XmlElement("dFecFirma")]
        public string FechaFirmaString
        {
            get => FechaFirma.ToString("yyyy-MM-ddTHH:mm:ss"); 
            set => FechaFirma = DateTime.ParseExact(value, "yyyy-MM-ddTHH:mm:ss", null);
        }

        [XmlElement("dVerFor")]
        public string VersionFormato { get; set; } = "150";

        [XmlElement("gGroupTiEvt")]
        public GrupoTipoEvento GrupoTipoEvento { get; set; }

        public EventoDE() { }

        public EventoDE(string cdc, DateTime dFecFirma, int dNumTim, string dEst, string dPunExp, string dNumIn, string dNumFin, int iTiDE, string mOtEve)
        {
            Id = cdc;
            FechaFirma = dFecFirma;

            GrupoTipoEvento = new GrupoTipoEvento
            {
                EventoInutilizacion = new EventoInutilizacion
                {
                    NroTimbrado = dNumTim,
                    Establecimiento = dEst,
                    PuntoExpedicion = dPunExp,
                    NroInicio = dNumIn,
                    NroFin = dNumFin,
                    TipoDocumento = iTiDE,
                    MotivoEvento = mOtEve,
                }
            };
        }
    }

    public class GrupoTipoEvento
    {
        [XmlElement("rGeVeInu")]
        public EventoInutilizacion EventoInutilizacion { get; set; }
    }

    public class EventoInutilizacion
    {  
        [XmlElement("dNumTim")]
        public int NroTimbrado { get; set; }

        [XmlElement("dEst")]
        public string Establecimiento { get; set; }

        [XmlElement("dPunExp")]
        public string PuntoExpedicion { get; set; }

        [XmlElement("dNumIn")]
        public string NroInicio { get; set; }

        [XmlElement("dNumFin")]
        public string NroFin { get; set; }

        [XmlElement("iTiDE")]
        public int TipoDocumento { get; set; }

        [XmlElement("mOtEve")]
        public string MotivoEvento { get; set; }

    }
}