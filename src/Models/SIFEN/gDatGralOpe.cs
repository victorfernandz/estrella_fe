using System;
using System.Xml.Serialization;
using System.Xml;
using System.Globalization;

// Campos Generales del Documento Electrónico DE (D001-D299)
public class GDatGralOpe // Nodo padre A001
{
    [XmlIgnore] // Evita serializar el DateTime directamente
    public DateTime FechaHoraEmision { get; set; }

    [XmlElement("dFeEmiDE")]
    public string FechaHoraEmisionString
    {
        get => FechaHoraEmision.ToString("yyyy-MM-ddTHH:mm:ss");
        set => FechaHoraEmision = DateTime.ParseExact(value, "yyyy-MM-ddTHH:mm:ss", null);
    }

    [XmlElement("gOpeCom")]
    public GOpeCom OperacionComercial { get; set; }

    [XmlElement("gEmis")]
    public GEmis GrupoCamposEmisor { get; set; }

    [XmlElement("gDatRec")]
    public GDatRec GrupoDatosReceptor { get; set; }

    public GDatGralOpe(){}

    public GDatGralOpe(DateTime dFeEmiDE, string? iTipTra, string cMoneOpe, string dDesMoneOpe, string dRucEm, int dDVEmi, int iTipCont, string dNomEmi, string dDirEmi, int dNumCas, int cDepEmi, string dDesDepEmi, int cDisEmi,
        string dDesDisEmi, int cCiuEmi, string dDesCiuEmi, string dTelEmi, string dEmailE, int iNatRec, int? iTiContRec, string? dDirRec, int? dNumCasRec, int iTiOpe, string cPaisRec, string dDesPaisRe, string dNomRec, string? dRucRec, 
        int? dDVRec, string? dTelRec, string? dCelRec, string? dEmailRec, decimal dTiCam,
        string? iTipIDRec, string? dNumIDRec,
        List<ActividadEconomica> actividades = null, List<ObligacionAfectada> obligaciones = null)
    {
        FechaHoraEmision = dFeEmiDE;
        OperacionComercial = new GOpeCom(iTipTra, cMoneOpe, dDesMoneOpe, dTiCam, obligaciones);
        GrupoCamposEmisor = new GEmis(dRucEm, dDVEmi, iTipCont, dNomEmi, dDirEmi, dNumCas, cDepEmi, dDesDepEmi, cDisEmi, dDesDisEmi, cCiuEmi, dDesCiuEmi, dTelEmi, dEmailE);
        
        if (actividades != null && actividades.Any())
        {
            foreach (var actividad in actividades)
            {
                GrupoCamposEmisor.ActividadesEconomicas.Add(new GActEco(actividad.Codigo, actividad.Descripcion));
            }
        }

        GrupoDatosReceptor = new GDatRec(iNatRec, iTiContRec, dDirRec, dNumCasRec, iTiOpe, cPaisRec, dDesPaisRe, dNomRec, dRucRec, dDVRec, dTelRec, dCelRec, dEmailRec, iTipIDRec, dNumIDRec);
    }
}

// Campos inherentes a la operación comercial (D010-D099)
public class GOpeCom // Nodo padre D001
{
    [XmlElement("iTipTra", Order = 1)]
    public string? TipoTransaccion { get; set; }

    [XmlElement("dDesTipTra", Order = 2)]
    public string DescripcionTipoTransaccion { get; set; }

    [XmlElement("iTImp", Order = 3)]
    public int TipoImpuesto = 5;

    [XmlElement("dDesTImp", Order = 4)]
    public string DescripTipoImp = "IVA - Renta";

    [XmlElement("cMoneOpe", Order = 5)]
    public string MonedaOperacion { get; set; }

    [XmlElement("dDesMoneOpe", Order = 6)]
    public string DescripcionMoneda { get; set; }

    [XmlElement("iCondAnt", Order = 9)]
    public int CodigoAnticipo { get; set; } = 1;

    [XmlElement("dDesCondAnt", Order = 10)]
    public string DescripcionCodigoAnticipo { get; set; } = "Anticipo Global";

    [XmlElement("dCondTiCam", Order = 7)]
    public int CondicionTipoCambio { get; set; } = 1;

    [XmlIgnore]
    public decimal TipoCambio { get; set; }
    [XmlElement("dTiCam", Order = 8)]
    public string TipoCambioStr
    {
        get => TipoCambio.ToString("F4", CultureInfo.InvariantCulture);
        set => TipoCambio = decimal.Parse(value, CultureInfo.InvariantCulture);
    }

    [XmlElement("gOblAfe", Order = 11)]
    public List<GOblAfe> ObligacionesAfectadas { get; set; } = new List<GOblAfe>();

    public GOpeCom(){}

    public GOpeCom(string? iTipTra, string cMoneOpe, string dDesMoneOpe, decimal dCondTiCam, List<ObligacionAfectada> obligaciones = null)
    {
        TipoTransaccion = iTipTra;
        DescripcionTipoTransaccion = ObtenerDescripTipoTran(iTipTra);
        MonedaOperacion = cMoneOpe;
        DescripcionMoneda = dDesMoneOpe;
        TipoCambio = dCondTiCam;

        // Agregar obligaciones afectadas solo si la lista no es nula y tiene elementos
        if (obligaciones != null && obligaciones.Any())
        {
            foreach (var obligacion in obligaciones)
            {
                ObligacionesAfectadas.Add(new GOblAfe(obligacion.Codigo, obligacion.Descripcion));
            }
        }
    }

    public bool ShouldSerializeTipoCambio()
    {
        return MonedaOperacion != "PYG";
    }

    public bool ShouldSerializeCondicionTipoCambio()
    {
        return MonedaOperacion != "PYG";
    }

    public bool ShouldSerializeTipoCambioStr()
    {
        return MonedaOperacion != "PYG";
    }

    private string ObtenerDescripTipoTran(string? iTipTra)
    {
        return iTipTra switch
        {
            "1" => "Venta de mercadería",
            "2" => "Prestación de servicios",
            "3" => "Mixto (Venta de mercadería y servicios)",
            "4" => "Venta de activo fijo",
            "5" => "Venta de divisas",
            "6" => "Compra de divisas",
            "7" => "Promoción o entrega de muestras",
            "8" => "Donación",
            "9" => "Anticipo",
            "10" => "Compra de productos",
            "11" => "Compra de servicios",
            "12" => "Venta de crédito fiscal",
            null => null
        };
    }

    public bool ShouldSerializeDescripcionTipoTransaccion()
    {
        return DescripcionTipoTransaccion != null;
    }
};

// Campos que identifican al emisor del Documento Electrónico DE (D100-D129)
public class GEmis // Nodo padre D001
{
    [XmlElement("dRucEm", Order = 1)]
    public string RucEmisor { get; set; }

    [XmlElement("dDVEmi", Order = 2)]
    public int DigVeriEmisor { get; set; }

    [XmlElement("iTipCont", Order = 3)]
    public int TipoContri { get; set; }

    [XmlElement("dNomEmi", Order = 4)]
    public string NombreEmisor { get; set; }

    [XmlElement("dDirEmi", Order = 5)]
    public string DireccionEmisor { get; set; }

    [XmlElement("dNumCas", Order = 6)]
    public int NumeroCasaE { get; set; }

    [XmlElement("cDepEmi", Order = 7)]
    public int CodDepartEmisor { get; set; }

    [XmlElement("dDesDepEmi", Order = 8)]
    public string DesDepEmisor { get; set; }

    [XmlElement("cDisEmi", Order = 9)]
    public int CodDistEmisor { get; set; }

    [XmlElement("dDesDisEmi", Order = 10)]
    public string DesDistEmisor { get; set;} 

    [XmlElement("cCiuEmi", Order = 11)]
    public int CodCiudEmisor { get; set; }

    [XmlElement("dDesCiuEmi", Order = 12)]
    public string DesCiuEmisor { get; set; }

    [XmlElement("dTelEmi", Order = 13)]
    public string TelefEmisor { get; set; }

    [XmlElement("dEmailE", Order = 14)]
    public string EmailEmisor { get; set; }

    // Agregar la lista de actividades económicas
    [XmlElement("gActEco", Order = 15)]
    public List<GActEco> ActividadesEconomicas { get; set; } = new List<GActEco>();

    public GEmis(){}

    public GEmis(string dRucEm, int dDVEmi, int iTipCont, string dNomEmi, string dDirEmi, int dNumCas, int cDepEmi, string dDesDepEmi, int cDisEmi, string dDesDisEmi,
        int cCiuEmi, string dDesCiuEmi, string dTelEmi, string dEmailE)
    {
        RucEmisor = dRucEm;
        DigVeriEmisor = dDVEmi;
        TipoContri = iTipCont;
        NombreEmisor = dNomEmi;
        DireccionEmisor = dDirEmi;
        NumeroCasaE = dNumCas;
        CodDepartEmisor = cDepEmi;
        DesDepEmisor = dDesDepEmi;
        CodDistEmisor = cDisEmi;
        DesDistEmisor = dDesDisEmi;
        CodCiudEmisor = cCiuEmi;
        DesCiuEmisor = dDesCiuEmi;
        TelefEmisor = dTelEmi;
        EmailEmisor = dEmailE;
        ActividadesEconomicas = new List<GActEco>();
    }
}

// Campos que describen la actividad económica del emisor (D130-D139)
public class GActEco // Nodo padre D100
{
    [XmlElement("cActEco")]
    public string CodActiEconomica { get; set; }

    [XmlElement("dDesActEco")]
    public string DescActiEconomica { get; set;}

    public GActEco (){}

    public GActEco (string cActEco, string dDesActEco)
    {
        CodActiEconomica = cActEco;
        DescActiEconomica = dDesActEco;
    }
}

// Campos que identifican al receptor del Documento Electrónico DE (D200-D299)
public class GDatRec // Nodo padre D001
{
    [XmlElement("iNatRec")]
    public int NaturalezaReceptor { get; set; }

    [XmlElement("iTiOpe")]
    public int TipoOperacion { get; set; }

    [XmlElement("cPaisRec")]
    public string PaisReceptor { get; set; }

    [XmlElement("dDesPaisRe")]
    public string DescPaisReceptor { get; set; }

    [XmlElement("iTiContRec")]
    public int? TipoContReceptor { get; set; }

    [XmlElement("dRucRec")]
    public string? RucReceptor { get; set; }

    [XmlElement("dDVRec")]
    public int? DVReceptor { get; set; }

    [XmlElement("iTipIDRec")]
    public string? TipoDocumentoReceptor { get; set; }

    [XmlElement("dDTipIDRec")]
    public string? DescrTipoDocReceptor { get; set; }

    [XmlElement("dNumIDRec")]
    public string? NumeroDocReceptr { get; set; }

    [XmlElement("dNomRec")]
    public string NombreReceptor { get; set; }

    [XmlElement("dDirRec")]
    public string? DireccionReceptor { get; set; }

    [XmlElement("dNumCasRec")]
    public int? NumCasaRec { get; set; } = 0;

    [XmlElement("dTelRec")]
    public string? TelefReceptor { get; set; }

    [XmlElement("dCelRec")] 
    public string? CelularRecep { get; set; }

    [XmlElement("dEmailRec")]
    public string? EmailRecep { get; set; }

    public bool ShouldSerializeRucReceptor()
    {
        return NaturalezaReceptor == 1;
    }

    public bool ShouldSerializeDVReceptor()
    {
        return NaturalezaReceptor == 1;
    }

    public bool ShouldSerializeTipoDocumentoReceptor()
    {
        return NaturalezaReceptor == 2;
    }

    public bool ShouldSerializeDescrTipoDocReceptor()
    {
        return NaturalezaReceptor == 2;
    }

    public bool ShouldSerializeNumeroDocReceptr()
    {
        return NaturalezaReceptor == 2;
    }

    public bool ShouldSerializeTipoContReceptor()
    {
        return NaturalezaReceptor == 1;
    }

    public string ObtenerDescrTipoDocumento(string? iTipIDRec)
    {
        return iTipIDRec switch
        {
            "1" => "Cédula paraguaya",
            "2" => "Pasaporte",
            "3" => "Cédula extranjera",
            "4" => "Carnet de residencia",
            "5" => "Innominado",
            "6" => "Tarjeta Diplomática de exoneración fiscal",
            null => null
        };
    }

    public bool ShouldSerializeDireccionReceptor()
    {
        return TipoOperacion == 4;
    }

    public bool ShouldSerializeNumCasaRec()
    {
        return TipoOperacion == 4; 
    }

    public GDatRec() { }

    public GDatRec(int iNatRec, int? iTiContRec, string? dDirRec, int? dNumCasRec, int iTiOpe, string cPaisRec, string dDesPaisRe, string dNomRec, string? dRucRec, int? dDVRec, string? dTelRec, string? dCelRec, string? dEmailRec, string? iTipIDRec, string? dNumIDRec)
    {
        NaturalezaReceptor = iNatRec;
        TipoOperacion = iTiOpe;
        PaisReceptor = cPaisRec;
        DescPaisReceptor = dDesPaisRe;
        TipoContReceptor = iTiContRec;
        RucReceptor = dRucRec;
        DVReceptor = dDVRec;
        TelefReceptor = dTelRec;
        CelularRecep = dCelRec;
        EmailRecep = dEmailRec;
        NombreReceptor = dNomRec;
        TipoDocumentoReceptor = iTipIDRec;
        DescrTipoDocReceptor = ObtenerDescrTipoDocumento(iTipIDRec);
        NumeroDocReceptr = dNumIDRec;
        DireccionReceptor = dDirRec;
        NumCasaRec = dNumCasRec ?? 0;
    }
}

public class GOblAfe // (D030-D040) - Nodo padre D010
{
    [XmlElement("cOblAfe")]
    public int CodObligacionAfectada { get; set; }

    [XmlElement("dDesOblAfe")]
    public string DescObligacionAfectada { get; set;}

    public GOblAfe() {}

    public GOblAfe(int cOblAfe, string dDesOblAfe)
    {
        CodObligacionAfectada = cOblAfe;
        DescObligacionAfectada = dDesOblAfe;
    }
}