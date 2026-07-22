using System;
using System.Xml.Serialization;
using System.Xml;
using System.Globalization;

// Campos que describen los subtotales y totales de la transacción documentada (F001-F099)
public class GTotSub
{
    [XmlIgnore]
    public decimal SubtotalExenta { get; set; }
    [XmlElement("dSubExe")]
    public string SubtotalExentaStr
    {
        get => SubtotalExenta.ToString("F8", CultureInfo.InvariantCulture);
        set => SubtotalExenta = decimal.Parse(value, CultureInfo.InvariantCulture);
    }

    [XmlIgnore]
    public decimal SubtotalExonerado { get; set; }

    [XmlElement("dSubExo")]
    public string SubtotalExoneradoStr
    {
        get => SubtotalExonerado.ToString("F8", CultureInfo.InvariantCulture);
        set => SubtotalExonerado = decimal.Parse(value, CultureInfo.InvariantCulture);
    }

    public bool ShouldSerializeSubtotalExoneradoStr()
    {
        return SubtotalExonerado > 0;
    }

    [XmlIgnore]
    public decimal SubtotalTasa5 { get; set; }
    [XmlElement("dSub5")]
    public string SubtotalTasa5Str
    {
        get => SubtotalTasa5.ToString("F8", CultureInfo.InvariantCulture);
        set => SubtotalTasa5 = decimal.Parse(value, CultureInfo.InvariantCulture);
    }    

    [XmlIgnore]
    public decimal SubtotalTasa10 { get; set; }
    [XmlElement("dSub10")]
    public string SubtotalTasa10Str
    {
        get => SubtotalTasa10.ToString("F8", CultureInfo.InvariantCulture);
        set => SubtotalTasa10 = decimal.Parse(value, CultureInfo.InvariantCulture);
    }

    [XmlIgnore]
    public decimal TotalBrutoOperacion { get; set; }
    [XmlElement("dTotOpe")]
    public string TotalBrutoOperacionStr
    {
        get => TotalBrutoOperacion.ToString("F8", CultureInfo.InvariantCulture);
        set => TotalBrutoOperacion = decimal.Parse(value, CultureInfo.InvariantCulture);
    }

    [XmlIgnore]
    public decimal TotalDescuentoItem { get; set; }
    [XmlElement("dTotDesc")]
    public string TotalDescuentoItemStr
    {
        get => TotalDescuentoItem.ToString("F8", CultureInfo.InvariantCulture);
        set => TotalDescuentoItem = decimal.Parse(value, CultureInfo.InvariantCulture);
    }

    [XmlIgnore]
    public decimal TotalDescuentoGlobal { get; set; }
    [XmlElement("dTotDescGlotem")]
    public string TotalDescuentoGlobalStr
    {
        get => TotalDescuentoGlobal.ToString("F8", CultureInfo.InvariantCulture);
        set => TotalDescuentoGlobal = decimal.Parse(value, CultureInfo.InvariantCulture);
    }

    [XmlIgnore]
    public decimal TotalAnticipoItem { get; set; }
    [XmlElement("dTotAntItem")]
    public string TotalAnticipoItemStr
    {
        get => TotalAnticipoItem.ToString("F8", CultureInfo.InvariantCulture);
        set => TotalAnticipoItem = decimal.Parse(value, CultureInfo.InvariantCulture);
    }

    [XmlIgnore]
    public decimal TotalAnticipoGlobal { get; set; }
    [XmlElement("dTotAnt")]
    public string TotalAnticipoGlobalStr
    {
        get => TotalAnticipoGlobal.ToString("F8", CultureInfo.InvariantCulture);
        set => TotalAnticipoGlobal = decimal.Parse(value, CultureInfo.InvariantCulture);
    }

    [XmlIgnore]
    public decimal PorcentajeDescuentoGlobal {get; set; }
    [XmlElement("dPorcDescTotal")]
    public string PorcentajeDescuentoGlobalStr
    {
        get => PorcentajeDescuentoGlobal.ToString("F8", CultureInfo.InvariantCulture);
        set => PorcentajeDescuentoGlobal = decimal.Parse(value, CultureInfo.InvariantCulture);
    }

    [XmlIgnore]
    public decimal TotalDescuentoOperacion { get; set; }
    [XmlElement("dDescTotal")]
    public string TotalDescuentoOperacionStr
    {
        get => TotalDescuentoOperacion.ToString("F8", CultureInfo.InvariantCulture);
        set => TotalDescuentoOperacion = decimal.Parse(value, CultureInfo.InvariantCulture);
    }
    
    [XmlIgnore]
    public decimal TotalAnticipoOperacion { get; set; }
    [XmlElement("dAnticipo")]
    public string TotalAnticipoOperacionStr
    {
        get => TotalAnticipoOperacion.ToString("F8", CultureInfo.InvariantCulture);
        set => TotalAnticipoOperacion = decimal.Parse(value, CultureInfo.InvariantCulture);
    }

    [XmlIgnore]
    public decimal RedondeoOperacion { get; set;}
    [XmlElement("dRedon")]
    public string RedondeoOperacionStr
    {
        get => RedondeoOperacion.ToString("F8", CultureInfo.InvariantCulture);
        set => RedondeoOperacion = decimal.Parse(value, CultureInfo.InvariantCulture);
    }

    [XmlIgnore]
    public decimal ComisionOperacion { get; set; }
    [XmlElement("dComi")]
    public string ComisionOperacionStr
    {
        get => ComisionOperacion.ToString("F8", CultureInfo.InvariantCulture);
        set => ComisionOperacion = decimal.Parse(value, CultureInfo.InvariantCulture);
    }

    [XmlIgnore]
    public decimal TotalNetoOperacion { get; set; }

    [XmlElement("dTotGralOpe")]
    public string TotalNetoOperacionStr
    {
        get => TotalNetoOperacion.ToString("F8", CultureInfo.InvariantCulture);
        set => TotalNetoOperacion = decimal.Parse(value, CultureInfo.InvariantCulture);
    }

    [XmlIgnore]
    public decimal LiquidacionIVA5 { get; set; }

    [XmlElement("dIVA5")]
    public string LiquidacionIVA5Str
    {
        get => LiquidacionIVA5.ToString("F8", CultureInfo.InvariantCulture);
        set => LiquidacionIVA5 = decimal.Parse(value, CultureInfo.InvariantCulture);
    }

    public bool ShouldSerializeLiquidacionIVA5Str()
    {
        return LiquidacionIVA5 > 0;
    }

    [XmlIgnore]
    public decimal LiquidacionIVA10 { get; set; }
    [XmlElement("dIVA10")]
    public string LiquidacionIVA10Str
    {
        get => LiquidacionIVA10.ToString("F8", CultureInfo.InvariantCulture);
        set => LiquidacionIVA10 = decimal.Parse(value, CultureInfo.InvariantCulture);
    }

    [XmlIgnore]
    public decimal LiquidacionTotalIVA5 {get; set; }
    [XmlElement("dLiqTotIVA5")]
    public string LiquidacionTotalIVA5Str
    {
        get => LiquidacionTotalIVA5.ToString("F8", CultureInfo.InvariantCulture);
        set => LiquidacionTotalIVA5 = decimal.Parse(value, CultureInfo.InvariantCulture);
    }

    [XmlIgnore]
    public decimal LiquidacionTotalIVA10 { get; set; }
    [XmlElement("dLiqTotIVA10")]
    public string LiquidacionTotalIVA10Str
    {
        get => LiquidacionTotalIVA10.ToString("F8", CultureInfo.InvariantCulture);
        set => LiquidacionTotalIVA10 = decimal.Parse(value, CultureInfo.InvariantCulture);
    }

    [XmlIgnore]
    public decimal LiquidacionIVAComision { get; set; }
    [XmlElement("dIVAComi")]
    public string LiquidacionIVAComisionStr
    {
        get => LiquidacionIVAComision.ToString("F8", CultureInfo.InvariantCulture);
        set => LiquidacionIVAComision = decimal.Parse(value, CultureInfo.InvariantCulture);
    }

    [XmlIgnore]
    public decimal LiquidacionTotalIVA { get; set; }
    [XmlElement("dTotIVA")]
    public string LiquidacionTotalIVAStr
    {
        get => LiquidacionTotalIVA.ToString("F8", CultureInfo.InvariantCulture);
        set => LiquidacionTotalIVA = decimal.Parse(value, CultureInfo.InvariantCulture);
    }

    [XmlIgnore]
    public decimal TotalGravada5 { get; set; }

    [XmlElement("dBaseGrav5")]
    public string TotalGravada5Str
    {
        get => TotalGravada5.ToString("F8", CultureInfo.InvariantCulture);
        set => TotalGravada5 = decimal.Parse(value, CultureInfo.InvariantCulture);
    }

    public bool ShouldSerializeTotalGravada5Str()
    {
        return TotalGravada5 > 0;
    }

    [XmlIgnore]
    public decimal TotalGravada10 { get; set; }
    [XmlElement("dBaseGrav10")]
    public string TotalGravada10Str
    {
        get => TotalGravada10.ToString("F8", CultureInfo.InvariantCulture);
        set => TotalGravada10 = decimal.Parse(value, CultureInfo.InvariantCulture);
    }

    [XmlIgnore]
    public decimal TotalGravadaIVA { get; set; }
    [XmlElement("dTBasGraIVA")]
    public string TotalGravadaIVAStr
    {
        get => TotalGravadaIVA.ToString("F8", CultureInfo.InvariantCulture);
        set => TotalGravadaIVA = decimal.Parse(value, CultureInfo.InvariantCulture);
    }

    [XmlIgnore]
    public decimal? TotalGeneralOperacionGs { get; set; }
    [XmlElement("dTotalGs")]
    public string TotalGeneralOperacionGsStr
    {
        get => TotalGeneralOperacionGs?.ToString("F8", CultureInfo.InvariantCulture);
        set => TotalGeneralOperacionGs = decimal.Parse(value, CultureInfo.InvariantCulture);
    }

    public bool ShouldSerializeTotalGeneralOperacionGsStr() => TotalGeneralOperacionGs != 0;

    public GTotSub(){}
}