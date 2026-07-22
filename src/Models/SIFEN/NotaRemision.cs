public class NotaRemision
{
    public int DocEntry { get; set; }
    public string DocType { get; set; }
    public string U_EXX_FE_CDC { get; set; }
    public string U_EXX_FE_Estado { get; set; }
    public string U_EXX_FE_CODERR { get; set; }
    public string U_CDOC { get; set; }
    public string CardCode { get; set; }
    public string U_EST { get; set; }
    public string U_PDE { get; set; }
    public string FolioNum { get; set; }
    public string DocDate { get; set; }
    public int DocTime { get; set; }
    public string U_FITE { get; set; }
    public int U_TIM { get; set; }
    public decimal dTiCam { get; set; }
    // Referencia opcional a la Factura relacionada (documento asociado, opcional en la NRE)
    public string U_NUMFC { get; set; }
    public int? timbradoSAP { get; set; }
    public string Comments { get; set; }

    public BusinessPartner BusinessPartner { get; set; }
    public Currencies Currencies { get; set; }
    public List<Item> Items { get; set; } = new List<Item>();

    public DatosNRE DatosNRE { get; set; }
}

// @EPY_NRDE - datos propios de la Nota de Remisión (gCamNRE + gTransp)
public class DatosNRE
{
    // gCamNRE (E500-E506)
    public string MotivoEmisionSAP { get; set; }
    public string ResponsableEmisionSAP { get; set; }
    public decimal? KmRecorrido { get; set; }
    public DateTime? FechaFuturaFactura { get; set; }

    // gTransp (E900-E912)
    public string TipoTransporteSAP { get; set; }
    public string ModalidadTransporteSAP { get; set; }
    public string ResponsableFleteSAP { get; set; }
    public string CondicionNegociacion { get; set; }
    public string NumeroManifiesto { get; set; }
    public string NumeroDespachoImportacion { get; set; }
    public DateTime? FechaInicioTraslado { get; set; }
    public DateTime? FechaFinTraslado { get; set; }
    public string PaisDestino { get; set; }
    public string PaisDestinoDescripcion { get; set; }

    public Transportista Transportista { get; set; }
    public Vehiculo Vehiculo { get; set; }
    public LocalSalida LocalSalida { get; set; }
}

// @EPY_TRAN - transportista / chofer / agente (gCamTrans, E980-E999)
public class Transportista
{
    public string NaturalezaSAP { get; set; }
    public string TipoDocIdentidadSAP { get; set; }
    public string DireccionAgente { get; set; }
    public string DireccionChofer { get; set; }
    public string DomicilioFiscal { get; set; }
    public int? DVAgente { get; set; }
    public int? DVTransportista { get; set; }
    public string NombreChofer { get; set; }
    public string Nacionalidad { get; set; }
    public string NumeroDocChofer { get; set; }
    public string NumeroDocTransportista { get; set; }
    public string NombreAgente { get; set; }
    public string NombreTransportista { get; set; }
    public string RucAgente { get; set; }
    public string RucTransportista { get; set; }
}

// @EPY_VEHI - vehículo de traslado (gVehTras, E960-E979)
public class Vehiculo
{
    public string DatosAdicionales { get; set; }
    public string TipoIdentificacionSAP { get; set; }
    public string Marca { get; set; }
    public string NumeroIdentificacion { get; set; }
    public string NumeroMatricula { get; set; }
    public string NumeroVuelo { get; set; }
    public string TipoVehiculoSAP { get; set; }
}

// OWHS - local de salida de la mercadería (gCamSal, E920-E939)
public class LocalSalida
{
    public string Direccion { get; set; }
    public string NumeroCasa { get; set; }
    public string CodDepartamento { get; set; }
    public string DescDepartamento { get; set; }
    public string CodDistrito { get; set; }
    public string DescDistrito { get; set; }
    public string CodCiudad { get; set; }
    public string DescCiudad { get; set; }
}
