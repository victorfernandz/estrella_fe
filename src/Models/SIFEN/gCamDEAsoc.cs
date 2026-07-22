using System;
using System.Xml.Serialization;
using System.Xml;

// H. Campos que identifican al documento asociado (H001-H049)
public class GCamDEAsoc // Nodo padre A001
{
    [XmlElement("iTipDocAso")]
    public int? TipoDocAsociado { get; set; }

    [XmlElement("dDesTipDocAso")]
    public string? DescTipoDocAsociado { get; set; }

    [XmlElement("dCdCDERef")]
    public string? CDCReferencia { get; set; }

    [XmlElement("dNTimDI")]
    public int? TimbradoDocImpreso { get; set; }

    [XmlElement("dEstDocAso")]
    public string? EstDocAsociado { get; set; }

    [XmlElement("dPExpDocAso")]
    public string? PuntoEmisionDocAsociado { get; set; }

    [XmlElement("dNumDocAso")]
    public string? NumeroDocAsociado { get; set; }

    [XmlElement("iTipoDocAso")]
    public int? TipoDocImpreso { get; set; } = 1;

    [XmlElement("dDTipoDocAso")]
    public string? DescTipoDocImpreso { get; set; }

    [XmlIgnore]
    public DateTime? FechaEmisionDocImpreso { get; set; }

    [XmlElement("dFecEmiDI")]
    public string FechaEmisionDocImpresoStr
    {
        get => FechaEmisionDocImpreso?.ToString("yyyy-MM-dd");
        set => FechaEmisionDocImpreso = string.IsNullOrWhiteSpace(value) ? null : DateTime.Parse(value);
    }

    public GCamDEAsoc (){}

    public GCamDEAsoc(int? iTipDocAso, string? dCdCDERef, int? dNTimDI, string? dEstDocAso, string? dPExpDocAso, string? dNumDocAso, int? iTipoDocAso, DateTime? dFecEmiDI)
    {
        TipoDocAsociado = iTipDocAso;
        DescTipoDocAsociado = ObtenerDescrTipoDocumento(iTipDocAso);
        CDCReferencia = dCdCDERef;
        TimbradoDocImpreso = dNTimDI;
        EstDocAsociado = dEstDocAso;
        PuntoEmisionDocAsociado = dPExpDocAso;
        NumeroDocAsociado = dNumDocAso;
        TipoDocImpreso = iTipoDocAso;
        DescTipoDocImpreso = ObtenerDescrTipoDocImpreso(iTipoDocAso);
        FechaEmisionDocImpreso = dFecEmiDI;
    }

    private string ObtenerDescrTipoDocumento(int? iTipDocAso)
    {
        return iTipDocAso switch
        {
            1 => "Electrónico",
            2 => "Impreso",
            3 => "Constancia Electrónica"
        };
    }

    public bool ShouldSerializedCDCReferencia()
    {
        return TipoDocAsociado == 1;
    }

    public bool ShouldSerializeTimbradoDocImpreso()
    {
        return TipoDocAsociado == 2;
    }

    public bool ShouldSerializeEstDocAsociado()
    {
        return TipoDocAsociado == 2;
    }

    public bool ShouldSerializePuntoEmisionDocAsociado()
    {
        return TipoDocAsociado == 2;
    }

    public bool ShouldSerializeNumeroDocAsociado()
    {
        return TipoDocAsociado == 2;
    }

    public bool ShouldSerializeTipoDocImpreso()
    {
        return TipoDocAsociado == 2;
    }

    public bool ShouldSerializeDescTipoDocImpreso()
     {
        return TipoDocAsociado == 2;
    }

    public string ObtenerDescrTipoDocImpreso(int? iTipoDocAso)
    {
        return iTipoDocAso switch
        {
            1 => "Factura",
            2 => "Nota de crédito",
            3 => "Nota de débito",
            4 => "Nota de remisión",
            5 => "Comprobante de retención",
            null => null
        };
    }

    public bool ShouldSerializeFechaEmisionDocImpresoStr()
    {
        return TipoDocAsociado == 2;
    }
}