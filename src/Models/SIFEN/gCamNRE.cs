using System;
using System.Xml.Serialization;
using System.Xml;
using System.Globalization;

// E6. Campos que componen la Nota de Remisión Electrónica (E500-E599)
public class GCamNRE // Nodo padre E001
{
    [XmlElement("iMotEmiNR")]
    public int MotivoEmision { get; set; }

    [XmlElement("dDesMotEmiNR")]
    public string DescMotivoEmision { get; set; }

    [XmlElement("iRespEmiNR")]
    public int ResponsableEmision { get; set; }

    [XmlElement("dDesRespEmiNR")]
    public string DescResponsableEmision { get; set; }

    [XmlIgnore]
    public decimal? KmRecorrido { get; set; }

    [XmlElement("dKmR")]
    public string KmRecorridoStr
    {
        get => KmRecorrido?.ToString(CultureInfo.InvariantCulture);
        set => KmRecorrido = string.IsNullOrWhiteSpace(value) ? null : decimal.Parse(value, CultureInfo.InvariantCulture);
    }

    public bool ShouldSerializeKmRecorridoStr() => KmRecorrido.HasValue;

    [XmlIgnore]
    public DateTime? FechaFuturaFactura { get; set; }

    // Fecha futura de emisión de la factura, cuando aún no fue emitida (E506)
    [XmlElement("dFecEm")]
    public string FechaFuturaFacturaStr
    {
        get => FechaFuturaFactura?.ToString("yyyy-MM-dd");
        set => FechaFuturaFactura = string.IsNullOrWhiteSpace(value) ? null : DateTime.Parse(value);
    }

    public bool ShouldSerializeFechaFuturaFacturaStr() => FechaFuturaFactura.HasValue;

    public GCamNRE() { }

    public GCamNRE(int iMotEmiNR, int iRespEmiNR, decimal? dKmR = null, DateTime? dFecEm = null)
    {
        MotivoEmision = iMotEmiNR;
        DescMotivoEmision = ObtenerDescMotivoEmision(iMotEmiNR);
        ResponsableEmision = iRespEmiNR;
        DescResponsableEmision = ObtenerDescResponsableEmision(iRespEmiNR);
        KmRecorrido = dKmR;
        FechaFuturaFactura = dFecEm;
    }

    private string ObtenerDescMotivoEmision(int iMotEmiNR)
    {
        return iMotEmiNR switch
        {
            1 => "Traslado por venta",
            2 => "Traslado por consignación",
            3 => "Exportación",
            4 => "Traslado por compra",
            5 => "Importación",
            6 => "Traslado por devolución",
            7 => "Traslado entre locales de la empresa",
            8 => "Traslado de bienes por transformación",
            9 => "Traslado de bienes por reparación",
            10 => "Traslado por emisor móvil",
            11 => "Exhibición o demostración",
            12 => "Participación en ferias",
            13 => "Traslado de encomienda",
            14 => "Decomiso",
            99 => "Otro",
            _ => null
        };
    }

    private string ObtenerDescResponsableEmision(int iRespEmiNR)
    {
        return iRespEmiNR switch
        {
            1 => "Emisor de la factura",
            2 => "Poseedor de la factura y bienes",
            3 => "Empresa transportista",
            4 => "Despachante de Aduanas",
            5 => "Agente de transporte o intermediario",
            _ => null
        };
    }
}

// E10. Campos que describen el transporte de las mercaderías (E900-E919)
public class GTransp // Nodo padre E001
{
    [XmlElement("iTipTrans")]
    public int? TipoTransporte { get; set; }

    [XmlElement("dDesTipTrans")]
    public string DescTipoTransporte { get; set; }

    public bool ShouldSerializeTipoTransporte() => TipoTransporte.HasValue;
    public bool ShouldSerializeDescTipoTransporte() => TipoTransporte.HasValue;

    [XmlElement("iModTrans")]
    public int ModalidadTransporte { get; set; }

    [XmlElement("dDesModTrans")]
    public string DescModalidadTransporte { get; set; }

    [XmlElement("iRespFlete")]
    public int ResponsableFlete { get; set; }

    // Condición de negociación (Incoterms - Tabla 10)
    [XmlElement("cCondNeg")]
    public string CondicionNegociacion { get; set; }

    public bool ShouldSerializeCondicionNegociacion() => !string.IsNullOrEmpty(CondicionNegociacion);

    // Nro. de manifiesto/conocimiento de carga/declaración de tránsito aduanero/carta de porte internacional
    [XmlElement("dNuManif")]
    public string NumeroManifiesto { get; set; }

    public bool ShouldSerializeNumeroManifiesto() => !string.IsNullOrEmpty(NumeroManifiesto);

    // Obligatorio si el motivo de emisión (E501) de la NRE es Importación
    [XmlElement("dNuDespImp")]
    public string NumeroDespachoImportacion { get; set; }

    public bool ShouldSerializeNumeroDespachoImportacion() => !string.IsNullOrEmpty(NumeroDespachoImportacion);

    [XmlIgnore]
    public DateTime FechaInicioTraslado { get; set; }

    [XmlElement("dIniTras")]
    public string FechaInicioTrasladoStr
    {
        get => FechaInicioTraslado.ToString("yyyy-MM-dd");
        set => FechaInicioTraslado = DateTime.Parse(value);
    }

    [XmlIgnore]
    public DateTime FechaFinTraslado { get; set; }

    [XmlElement("dFinTras")]
    public string FechaFinTrasladoStr
    {
        get => FechaFinTraslado.ToString("yyyy-MM-dd");
        set => FechaFinTraslado = DateTime.Parse(value);
    }

    [XmlElement("cPaisDest")]
    public string PaisDestino { get; set; }

    public bool ShouldSerializePaisDestino() => !string.IsNullOrEmpty(PaisDestino);

    [XmlElement("dDesPaisDest")]
    public string DescPaisDestino { get; set; }

    public bool ShouldSerializeDescPaisDestino() => !string.IsNullOrEmpty(PaisDestino);

    // E10.1 Local de salida de las mercaderías (E920-E939)
    [XmlElement("gCamSal")]
    public GCamSal LocalSalida { get; set; }

    public bool ShouldSerializeLocalSalida() => LocalSalida != null;

    // E10.2 Local(es) de entrega de las mercaderías (E940-E959) - hasta 99 ocurrencias
    [XmlElement("gCamEnt")]
    public List<GCamEnt> LocalesEntrega { get; set; } = new List<GCamEnt>();

    // E10.3 Vehículo(s) de traslado (E960-E979) - hasta 4 ocurrencias
    [XmlElement("gVehTras")]
    public List<GVehTras> Vehiculos { get; set; } = new List<GVehTras>();

    // E10.4 Transportista (E980-E999)
    [XmlElement("gCamTrans")]
    public GCamTrans Transportista { get; set; }

    public bool ShouldSerializeTransportista() => Transportista != null;

    public GTransp() { }

    public GTransp(int? iTipTrans, int iModTrans, int iRespFlete, DateTime dIniTras, DateTime dFinTras,
        string cCondNeg = null, string dNuManif = null, string dNuDespImp = null, string cPaisDest = null, string dDesPaisDest = null)
    {
        TipoTransporte = iTipTrans;
        DescTipoTransporte = ObtenerDescTipoTransporte(iTipTrans);
        ModalidadTransporte = iModTrans;
        DescModalidadTransporte = ObtenerDescModalidadTransporte(iModTrans);
        ResponsableFlete = iRespFlete;
        CondicionNegociacion = cCondNeg;
        NumeroManifiesto = dNuManif;
        NumeroDespachoImportacion = dNuDespImp;
        FechaInicioTraslado = dIniTras;
        FechaFinTraslado = dFinTras;
        PaisDestino = cPaisDest;
        DescPaisDestino = dDesPaisDest;
    }

    private string ObtenerDescTipoTransporte(int? iTipTrans)
    {
        return iTipTrans switch
        {
            1 => "Propio",
            2 => "Tercero",
            _ => null
        };
    }

    private string ObtenerDescModalidadTransporte(int iModTrans)
    {
        return iModTrans switch
        {
            1 => "Terrestre",
            2 => "Fluvial",
            3 => "Aéreo",
            4 => "Multimodal",
            _ => null
        };
    }
}

// E10.1. Campos que identifican el local de salida de las mercaderías (E920-E939)
public class GCamSal // Nodo padre E900
{
    [XmlElement("dDirLocSal")]
    public string Direccion { get; set; }

    [XmlElement("dNumCasSal")]
    public int NumeroCasa { get; set; }

    [XmlElement("dComp1Sal")]
    public string Complemento1 { get; set; }
    public bool ShouldSerializeComplemento1() => !string.IsNullOrEmpty(Complemento1);

    [XmlElement("dComp2Sal")]
    public string Complemento2 { get; set; }
    public bool ShouldSerializeComplemento2() => !string.IsNullOrEmpty(Complemento2);

    [XmlElement("cDepSal")]
    public int CodDepartamento { get; set; }

    [XmlElement("dDesDepSal")]
    public string DescDepartamento { get; set; }

    [XmlElement("cDisSal")]
    public int? CodDistrito { get; set; }
    public bool ShouldSerializeCodDistrito() => CodDistrito.HasValue;

    [XmlElement("dDesDisSal")]
    public string DescDistrito { get; set; }
    public bool ShouldSerializeDescDistrito() => CodDistrito.HasValue;

    [XmlElement("cCiuSal")]
    public int CodCiudad { get; set; }

    [XmlElement("dDesCiuSal")]
    public string DescCiudad { get; set; }

    [XmlElement("dTelSal")]
    public string Telefono { get; set; }
    public bool ShouldSerializeTelefono() => !string.IsNullOrEmpty(Telefono);

    public GCamSal() { }
}

// E10.2. Campos que identifican el local de entrega de las mercaderías (E940-E959)
public class GCamEnt // Nodo padre E900
{
    [XmlElement("dDirLocEnt")]
    public string Direccion { get; set; }

    [XmlElement("dNumCasEnt")]
    public int NumeroCasa { get; set; }

    [XmlElement("dComp1Ent")]
    public string Complemento1 { get; set; }
    public bool ShouldSerializeComplemento1() => !string.IsNullOrEmpty(Complemento1);

    [XmlElement("dComp2Ent")]
    public string Complemento2 { get; set; }
    public bool ShouldSerializeComplemento2() => !string.IsNullOrEmpty(Complemento2);

    [XmlElement("cDepEnt")]
    public int CodDepartamento { get; set; }

    [XmlElement("dDesDepEnt")]
    public string DescDepartamento { get; set; }

    [XmlElement("cDisEnt")]
    public int? CodDistrito { get; set; }
    public bool ShouldSerializeCodDistrito() => CodDistrito.HasValue;

    [XmlElement("dDesDisEnt")]
    public string DescDistrito { get; set; }
    public bool ShouldSerializeDescDistrito() => CodDistrito.HasValue;

    [XmlElement("cCiuEnt")]
    public int CodCiudad { get; set; }

    [XmlElement("dDesCiuEnt")]
    public string DescCiudad { get; set; }

    [XmlElement("dTelEnt")]
    public string Telefono { get; set; }
    public bool ShouldSerializeTelefono() => !string.IsNullOrEmpty(Telefono);

    public GCamEnt() { }
}

// E10.3. Campos que identifican el vehículo de traslado de mercaderías (E960-E979)
public class GVehTras // Nodo padre E900
{
    [XmlElement("dTiVehTras")]
    public string TipoVehiculo { get; set; }

    [XmlElement("dMarVeh")]
    public string Marca { get; set; }

    // 1 = Número de identificación del vehículo, 2 = Número de matrícula del vehículo (E967)
    [XmlElement("dTipIdenVeh")]
    public int TipoIdentificacion { get; set; }

    [XmlElement("dNroIDVeh")]
    public string NumeroIdentificacion { get; set; }
    public bool ShouldSerializeNumeroIdentificacion() => TipoIdentificacion == 1;

    [XmlElement("dAdicVeh")]
    public string DatosAdicionales { get; set; }
    public bool ShouldSerializeDatosAdicionales() => !string.IsNullOrEmpty(DatosAdicionales);

    [XmlElement("dNroMatVeh")]
    public string NumeroMatricula { get; set; }
    public bool ShouldSerializeNumeroMatricula() => TipoIdentificacion == 2;

    // Obligatorio si la modalidad de transporte (E903/iModTrans) es Aéreo
    [XmlElement("dNroVuelo")]
    public string NumeroVuelo { get; set; }
    public bool ShouldSerializeNumeroVuelo() => !string.IsNullOrEmpty(NumeroVuelo);

    public GVehTras() { }
}

// E10.4. Campos que identifican al transportista (persona física o jurídica) (E980-E999)
public class GCamTrans // Nodo padre E900
{
    // 1 = Contribuyente, 2 = No contribuyente (E981)
    [XmlElement("iNatTrans")]
    public int NaturalezaTransportista { get; set; }

    [XmlElement("dNomTrans")]
    public string NombreTransportista { get; set; }

    [XmlElement("dRucTrans")]
    public string RucTransportista { get; set; }
    public bool ShouldSerializeRucTransportista() => NaturalezaTransportista == 1;

    [XmlElement("dDVTrans")]
    public int? DVTransportista { get; set; }
    public bool ShouldSerializeDVTransportista() => NaturalezaTransportista == 1;

    // 1=Cédula paraguaya, 2=Pasaporte, 3=Cédula extranjera, 4=Carnet de residencia (E985)
    [XmlElement("iTipIDTrans")]
    public int? TipoDocIdentidad { get; set; }
    public bool ShouldSerializeTipoDocIdentidad() => NaturalezaTransportista == 2;

    [XmlElement("dDTipIDTrans")]
    public string DescTipoDocIdentidad { get; set; }
    public bool ShouldSerializeDescTipoDocIdentidad() => NaturalezaTransportista == 2;

    [XmlElement("dNumIDTrans")]
    public string NumeroDocIdentidad { get; set; }
    public bool ShouldSerializeNumeroDocIdentidad() => NaturalezaTransportista == 2;

    [XmlElement("cNacTrans")]
    public string Nacionalidad { get; set; }
    public bool ShouldSerializeNacionalidad() => !string.IsNullOrEmpty(Nacionalidad);

    [XmlElement("dDesNacTrans")]
    public string DescNacionalidad { get; set; }
    public bool ShouldSerializeDescNacionalidad() => !string.IsNullOrEmpty(Nacionalidad);

    [XmlElement("dNumIDChof")]
    public string NumeroDocChofer { get; set; }

    [XmlElement("dNomChof")]
    public string NombreChofer { get; set; }

    [XmlElement("dDomFisc")]
    public string DomicilioFiscal { get; set; }
    public bool ShouldSerializeDomicilioFiscal() => !string.IsNullOrEmpty(DomicilioFiscal);

    [XmlElement("dDirChof")]
    public string DireccionChofer { get; set; }
    public bool ShouldSerializeDireccionChofer() => !string.IsNullOrEmpty(DireccionChofer);

    [XmlElement("dNombAg")]
    public string NombreAgente { get; set; }
    public bool ShouldSerializeNombreAgente() => !string.IsNullOrEmpty(NombreAgente);

    [XmlElement("dRucAg")]
    public string RucAgente { get; set; }
    public bool ShouldSerializeRucAgente() => !string.IsNullOrEmpty(RucAgente);

    [XmlElement("dDVAg")]
    public int? DVAgente { get; set; }
    public bool ShouldSerializeDVAgente() => DVAgente.HasValue;

    [XmlElement("dDirAge")]
    public string DireccionAgente { get; set; }
    public bool ShouldSerializeDireccionAgente() => !string.IsNullOrEmpty(DireccionAgente);

    public GCamTrans() { }
}
