using System;
using System.Xml.Serialization;
using System.Xml;

//[XmlRoot("rEnviEventoDe")]
[XmlRoot("rEnviEventoDe", Namespace = "http://ekuatia.set.gov.py/sifen/xsd")]

public class DocumentoEventoCancelacion
{
    public DocumentoEventoCancelacion()
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

    public DocumentoEventoCancelacion(string cdc, DateTime dFecFirma, string mOtEve)
    {
        dId = cdc;
        dEvReg = new DEvReg
        {
            GrupoEventos = new GGroupGesEve
            {
                GestionEvento = new RGesEve
                {
                    rEve = new EventoDE(cdc, dFecFirma, mOtEve)
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

        public EventoDE(string cdc, DateTime dFecFirma, string mOtEve)
        {
            Id = cdc;
            FechaFirma = dFecFirma;

            GrupoTipoEvento = new GrupoTipoEvento
            {
                EventoCancelacion = new EventoCancelacion
                {
                    cdc = cdc,
                    motivo = mOtEve
                }
            };
        }
    }

    public class GrupoTipoEvento
    {
        [XmlElement("rGeVeCan")]
        public EventoCancelacion EventoCancelacion { get; set; }
    }

    public class EventoCancelacion
    {
        [XmlElement("Id")]
        public string cdc { get; set; }

        [XmlElement("mOtEve")]
        public string motivo { get; set; }
    }
}