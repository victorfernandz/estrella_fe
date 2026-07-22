using System;
using System.Xml.Serialization;
using System.Xml;
using System.Globalization;

// Campos específicos por tipo de Documento Electrónico (E001-E009)
public class GDtipDE // Nodo padre A001
{
    [XmlElement("gCamFE")]
    public GCamFE CamposFacturaElectronica { get; set; }

    [XmlElement("gCamCond")]
    public GCamCond CondicionOperacion { get; set; }

    [XmlElement("gPaConEIni")]
    public GPaConEIni PagoContadoInicial { get; set; }

    [XmlElement("gCamNCDE")]
    public GCamNCDE CamposNotaCreditoElectronica { get; set; }

    [XmlElement("gCamNRE")]
    public GCamNRE CamposNotaRemisionElectronica { get; set; }

    [XmlElement("gCamItem")]
    public List<GCamItem> Items { get; set; } = new List<GCamItem>();

    [XmlElement("gTransp")]
    public GTransp GrupoTransporte { get; set; }

    public GDtipDE() {}

    public GDtipDE(int? iIndPres, int? iCondOpe, int? iCondCred, int? iTiPago, decimal? dMonTiPag, string? cMoneTiPag, string dDMoneTiPag, decimal? dTiCamTiPag)
    {
        CamposFacturaElectronica = new GCamFE(iIndPres);
        CondicionOperacion = new GCamCond(iCondOpe, iCondCred, null, null, iTiPago, dMonTiPag, cMoneTiPag, dDMoneTiPag, dTiCamTiPag ?? 0);

        Items = new List<GCamItem>();
    }

    public bool ShouldSerializeCamposNotaCreditoElectronica()
    {
        return CamposNotaCreditoElectronica != null;
    }

    public bool ShouldSerializeCamposNotaRemisionElectronica()
    {
        return CamposNotaRemisionElectronica != null;
    }

    public bool ShouldSerializeGrupoTransporte()
    {
        return GrupoTransporte != null;
    }
}

// Campos que componen la Factura Electrónica FE (E010-E099)
public class GCamFE // Nodo padre E001
{
    [XmlElement("iIndPres")]
    public int? IndicadorPresencia { get; set; }

    [XmlElement("dDesIndPres")]
    public string DescripcionIndicadorPresencia { get; set; }

    public GCamFE() {}

    public GCamFE(int? iIndPres)
    {
        IndicadorPresencia = iIndPres;
        DescripcionIndicadorPresencia = ObtenerDescripcionIndicadorPresencia(iIndPres);
    }

    private string ObtenerDescripcionIndicadorPresencia(int? iIndPres)
    {
        return iIndPres switch
        {
            1 => "Operación presencial",
            2 => "Operación electrónica",
            3 => "Operación telemarketing",
            4 => "Venta a domicilio",
            5 => "Operación bancaria",
            6 => "Operación cíclica",
            9 => "Otro",
            null => null
        };
    }
}

// Campos que componen la Nota de Crédito/Débito Electrónica NCE-NDE (E400-E499)
public class GCamNCDE
{
    [XmlElement("iMotEmi")]
    public int? MotivoEmision { get; set; }

    [XmlElement("dDesMotEmi")]
    public string DescrMotivoEmision { get; set; }

    public GCamNCDE() { }

    public GCamNCDE(int? iMotEmi)
    {
        MotivoEmision = iMotEmi;
        DescrMotivoEmision = ObtenerDescrMotivoEmision(iMotEmi);
    }

    public string ObtenerDescrMotivoEmision(int? iMotEmi)
    {
        if (iMotEmi == null)
            return null;

        switch (iMotEmi)
        {
            case 1: return "Devolución y Ajuste de precios";
            case 2: return "Devolución";
            case 3: return "Descuento";
            case 4: return "Bonificación";
            case 5: return "Crédito incobrable";
            case 6: return "Recupero de costo";
            case 7: return "Recupero de gasto";
            case 8: return "Ajuste de precio";
            default: return null;
        }
    }

}

// Campos que describen la condición de la operación (E600-E699)
public class GCamCond // Nodo padre E001
{
    [XmlElement("iCondOpe")]
    public int? CondicionOperacion { get; set; }

    [XmlElement("dDCondOpe")]
    public string DescCondicionOperacion { get; set; }

    [XmlElement("gPagCred")]
    public GPagCred OperacionCredito { get; set; }

    [XmlElement("gPaConEIni")]
    public GPaConEIni PagoContadoInicial { get; set; }

    public GCamCond() { }

    public GCamCond(int? iCondOpe, int? iCondCred, string dPlazoCre, int? dCuotas, int? iTiPago, decimal? dMonTiPag, string? cMoneTiPag, string dDMoneTiPag, decimal dTiCamTiPag)
    {
        CondicionOperacion = iCondOpe;
        DescCondicionOperacion = ObtenerDescCondOperacion(iCondOpe);

        // Si la condición es "Crédito" y se proporcionan datos adicionales, inicializar gPagCred
        if (iCondOpe == 1 && iTiPago.HasValue && dMonTiPag.HasValue)
        {
            PagoContadoInicial = new GPaConEIni(iTiPago.Value, dMonTiPag.Value, cMoneTiPag, dDMoneTiPag, dTiCamTiPag);
        }
        else if (iCondOpe == 2)
        {
            OperacionCredito = new GPagCred(iCondCred ?? 1, dPlazoCre, dCuotas);
        }
    }

    private string ObtenerDescCondOperacion(int? iCondOpe)
    {
        return iCondOpe switch
        {
            1 => "Contado",
            2 => "Crédito"
        };
    }

    // Método para controlar si se serializa gPagCred
    public bool ShouldSerializeOperacionCredito()
    {
        return CondicionOperacion == 2; // Solo serializar si es crédito
    }

    public bool ShouldSerializePagoContadoInicial()
    {
        return CondicionOperacion == 1 && PagoContadoInicial != null;
    }
}

// Campos que describen la forma de pago de la operación al contado o del monto de la entrega inicial (E605-E619)
public class GPaConEIni // Nodo Padre E600
{
    [XmlElement("iTiPago")]
    public int TipoPago { get; set; }

    [XmlElement("dDesTiPag")]
    public string DescripcionTipoPago { get; set; }

    [XmlIgnore]
    public decimal? MontoTipoPago { get; set; }
    [XmlElement("dMonTiPag")]
    public string MontoTipoPagoStr
    {
        get => MontoTipoPago?.ToString("F4", CultureInfo.InvariantCulture);
        set => MontoTipoPago = decimal.Parse(value, CultureInfo.InvariantCulture);
    }

    [XmlElement("cMoneTiPag")]
    public string? MonedaTipoPago { get; set; }

    [XmlElement("dDMoneTiPag")]
    public string? DescripcionMonedaTipoPago { get; set; }

    [XmlIgnore]
    public decimal? TipoCambioPago { set; get; }
    [XmlElement("dTiCamTiPag")]
    public string TipoCambioPagoStr
    {
        get => TipoCambioPago?.ToString("F4", CultureInfo.InvariantCulture);
        set => TipoCambioPago = decimal.Parse(value, CultureInfo.InvariantCulture);
    }

    public bool ShouldSerializeTipoCambioPagoStr()
    {
        return MonedaTipoPago != "PYG";
    }

    public GPaConEIni() { }

    public GPaConEIni(int iTiPago, decimal? dMonTiPag, string? cMoneTiPag, string? dDMoneTiPag, decimal? dTiCamTiPag = null)
    {
        TipoPago = iTiPago;
        DescripcionTipoPago = ObtenerDescripcionTipoPago(iTiPago);
        MontoTipoPago = dMonTiPag;
        MonedaTipoPago = cMoneTiPag;
        DescripcionMonedaTipoPago = dDMoneTiPag;
        TipoCambioPago = dTiCamTiPag;
    }

    private string ObtenerDescripcionTipoPago(int iTiPago)
    {
        return iTiPago switch
        {
            1 => "Efectivo",
            2 => "Cheque",
            3 => "Tarjeta de crédito",
            4 => "Tarjeta de débito",
            5 => "Transferencia",
            6 => "Giro",
            7 => "Billetera electrónica",
            8 => "Tarjeta empresarial",
            9 => "Vale",
            10 => "Retención",
            11 => "Pago por anticipo",
            12 => "Valor fiscal",
            13 => "Valor comercial",
            14 => "Compensación",
            15 => "Permuta",
            16 => "Pago bancario",
            17 => "Pago Móvil",
            18 => "Donación",
            19 => "Promoción",
            20 => "Consumo Interno",
            21 => "Pago Electrónico",
            99 => "Otro"
        };
    }
}

// Campos que describen la operación a crédito (E640-E649)
public class GPagCred // Nodo padre E600
{
    [XmlElement("iCondCred")]
    public int CondicionCredito { get; set; }

    [XmlElement("dDCondCred")]
    public string DescripcionCondicionCredito { get; set; }

    [XmlElement("dPlazoCre")]
    public string? PlazoCredito { get; set; }

    [XmlElement("dCuotas")]
    public int? CantidadCuotas { get; set; }

    [XmlElement("gCuotas")]
    public List<GCuotas> Cuotas { get; set; }

    public GPagCred()
    {
        Cuotas = new List<GCuotas>();
    }

    public GPagCred(int iCondCred, string? dPlazoCre, int? dCuotas = null)
    {
        CondicionCredito = iCondCred;
        DescripcionCondicionCredito = ObtenerDescripcionCondicionCredito(iCondCred);
        Cuotas = new List<GCuotas>();

        // Asignar valores según la condición de crédito
        if (iCondCred == 1) // Plazo
        {
            PlazoCredito = dPlazoCre;
        }
        else if (iCondCred == 2) // Cuota
        {
            CantidadCuotas = dCuotas;
        }
    }

    private string ObtenerDescripcionCondicionCredito(int iCondCred)
    {
        return iCondCred switch
        {
            1 => "Plazo",
            2 => "Cuota"
        };
    }

    // Métodos para controlar si se serializan campos opcionales
    public bool ShouldSerializePlazoCredito()
    {
        return CondicionCredito == 1 && !string.IsNullOrEmpty(PlazoCredito);
    }

    public bool ShouldSerializeCantidadCuotas()
    {
        return CondicionCredito == 2 && CantidadCuotas.HasValue;
    }

    public bool ShouldSerializeCuotas()
    {
        return Cuotas != null && Cuotas.Count > 0;
    }
}

// Campos que describen las cuotas (E650-E659)
public class GCuotas // Nodo padre E640
{
    [XmlElement("cMoneCuo")]
    public string MonedaCuota { get; set; }

    [XmlElement("dDMoneCuo")]
    public string DescripcionMonedaCuota { get; set; }

    [XmlElement("dMonCuota")]
    public decimal MontoCuota { get; set; }

    [XmlElement("dVencCuo")]
    public string FechaVencimientoCuota { get; set; }

    public GCuotas() { }

    public GCuotas(string cMoneCuo, string dDMoneCuo, decimal dMonCuota, DateTime dVencCuo)
    {
        MonedaCuota = cMoneCuo;
        DescripcionMonedaCuota = dDMoneCuo;
        MontoCuota = dMonCuota;
        FechaVencimientoCuota = dVencCuo.ToString("yyyy-MM-dd");
    }
}

// Campos que describen los ítems de la operación (E700-E899)
public class GCamItem // Nodo Padre E001
{
    [XmlElement("dCodInt")]
    public string CodigoItem { get; set; }
/*
    [XmlElement("dParAranc")]
    public int ParteArancelaria { get; set; } = 1111;
*/
    [XmlElement("dDesProSer")]
    public string DescripcionItem { get; set; }

    [XmlElement("cUniMed")]
    public int UnidadMedida { get; set; }

    [XmlElement("dDesUniMed")]
    public string DescripcionUnidadMedida { get; set; }

    [XmlIgnore]
    public decimal CantidadProducto { get; set; }

    [XmlElement("dCantProSer")]
    public string CantidadProductoString
    {
        get => CantidadProducto.ToString("F8", CultureInfo.InvariantCulture);
        set => CantidadProducto = decimal.Parse(value, CultureInfo.InvariantCulture);
    }

    // Código de datos de relevancia de las mercaderías (E715) - Obligatorio solo para Nota de Remisión, según RG 41/14 (tolerancia de quiebra/merma)
    [XmlElement("cRelMerc")]
    public int? CodigoRelevanciaMercaderia { get; set; }
    public bool ShouldSerializeCodigoRelevanciaMercaderia() => CodigoRelevanciaMercaderia.HasValue;

    [XmlElement("dDesRelMerc")]
    public string DescRelevanciaMercaderia { get; set; }
    public bool ShouldSerializeDescRelevanciaMercaderia() => CodigoRelevanciaMercaderia.HasValue;

    [XmlIgnore]
    public decimal? CantidadQuiebraMerma { get; set; }
    [XmlElement("dCanQuiMer")]
    public string CantidadQuiebraMermaStr
    {
        get => CantidadQuiebraMerma?.ToString("F4", CultureInfo.InvariantCulture);
        set => CantidadQuiebraMerma = string.IsNullOrWhiteSpace(value) ? null : decimal.Parse(value, CultureInfo.InvariantCulture);
    }
    public bool ShouldSerializeCantidadQuiebraMermaStr() => CantidadQuiebraMerma.HasValue;

    [XmlIgnore]
    public decimal? PorcentajeQuiebraMerma { get; set; }
    [XmlElement("dPorQuiMer")]
    public string PorcentajeQuiebraMermaStr
    {
        get => PorcentajeQuiebraMerma?.ToString("F8", CultureInfo.InvariantCulture);
        set => PorcentajeQuiebraMerma = string.IsNullOrWhiteSpace(value) ? null : decimal.Parse(value, CultureInfo.InvariantCulture);
    }
    public bool ShouldSerializePorcentajeQuiebraMermaStr() => PorcentajeQuiebraMerma.HasValue;

    [XmlElement("gValorItem")]
    public GValorItem ValorItem { get; set; }

    [XmlElement("gCamIVA")]
    public GCamIVA CamposIVA { get; set; }

    // La Nota de Remisión Electrónica (C002=7) no debe informar precio ni IVA por ítem (reglas E720a/E730a del Manual Técnico)
    [XmlIgnore]
    public bool EsNotaRemision { get; set; }
    public bool ShouldSerializeValorItem() => !EsNotaRemision;
    public bool ShouldSerializeCamposIVA() => !EsNotaRemision;

    public GCamItem()
    {
        ValorItem = new GValorItem();
        CamposIVA = new GCamIVA();
    }
}

// Campos que describen el precio, tipo de cambio y valor total de la operación por ítem (E720-E729)
public class GValorItem // Nodo Padre E700dCodInt
{
    [XmlIgnore]
    public decimal PrecioUnitario { get; set; }

    [XmlElement("dPUniProSer")]
    public string PrecioUnitarioString
    {
        get => PrecioUnitario.ToString("F8", CultureInfo.InvariantCulture);
        set => PrecioUnitario = decimal.Parse(value, CultureInfo.InvariantCulture);
    }

    [XmlElement("dTiCamIt")]
    public decimal TipoCambioIt { get; set; }

    [XmlIgnore]
    public string MonedaOperacion { get; set; }

    [XmlIgnore]
    public bool EsTipoCambioGlobal { get; set; } // Valor de dCondTiCam del documento (1=global, 2=individual)

    public bool ShouldSerializeTipoCambioIt()
    {
        return MonedaOperacion != "PYG" && !EsTipoCambioGlobal;
    }

    [XmlIgnore]
    public decimal TotalBrutoItem { get; set; }

    [XmlElement("dTotBruOpeItem")]
    public string TotalBrutoItemString
    {
        get => TotalBrutoItem.ToString("F8", CultureInfo.InvariantCulture);
        set => TotalBrutoItem = decimal.Parse(value, CultureInfo.InvariantCulture);
    }

    [XmlElement("gValorRestaItem")]
    public GValorRestaItem ValorRestaItem { get; set; }

    public GValorItem()
    {
        ValorRestaItem = new GValorRestaItem();
    }
}

// E8.1.1 Campos que describen los descuentos, anticipos y valor total por ítem (EA001-EA050)
public class GValorRestaItem // Nodo Padre E720
{
    [XmlIgnore]
    public decimal DescuentoItem { get; set; }
    [XmlElement("dDescItem")]
    public string DescuentoItemStr
    {
        get => DescuentoItem.ToString("F8", CultureInfo.InvariantCulture);
        set => DescuentoItem = decimal.Parse(value, CultureInfo.InvariantCulture);
    }
    
    [XmlIgnore]
    public decimal PorcentajeDescuentoItem { get; set; }
    [XmlElement("dPorcDesIt")]
    public string PorcentajeDescuentoItemStr
    {
        get => PorcentajeDescuentoItem.ToString("F8", CultureInfo.InvariantCulture);
        set => PorcentajeDescuentoItem = decimal.Parse(value, CultureInfo.InvariantCulture);
    }
    
    [XmlIgnore]
    public decimal DescuentoGlobalItem { get; set; }
    [XmlElement("dDescGloItem")]
    public string DescuentoGlobalItemStr
    {
        get => DescuentoGlobalItem.ToString("F8", CultureInfo.InvariantCulture);
        set => DescuentoGlobalItem = decimal.Parse(value, CultureInfo.InvariantCulture);
    }

    [XmlIgnore]
    public decimal AnticipoPreUnitarioItem { get; set; }    
    [XmlElement("dAntPreUniIt")]
    public string AnticipoPreUnitarioItemStr
    {
        get => AnticipoPreUnitarioItem.ToString("F8", CultureInfo.InvariantCulture);
        set => AnticipoPreUnitarioItem = decimal.Parse(value, CultureInfo.InvariantCulture);
    }    
    
    [XmlIgnore]
    public decimal AnticipoGlobalPreUnitarioItem { get; set; }
    [XmlElement("dAntGloPreUniIt")]
    public string AnticipoGlobalPreUnitarioItemStr
    {
        get => AnticipoGlobalPreUnitarioItem.ToString("F8", CultureInfo.InvariantCulture);
        set => AnticipoGlobalPreUnitarioItem = decimal.Parse(value, CultureInfo.InvariantCulture);
    }
        
    [XmlIgnore]
    public decimal TotalOperacionItem { get; set; }

    [XmlElement("dTotOpeItem")]
    public string TotalOperacionItemStr
    {
        get => TotalOperacionItem.ToString("F8", CultureInfo.InvariantCulture);
        set => TotalOperacionItem = decimal.Parse(value, CultureInfo.InvariantCulture);
    }

    [XmlIgnore]
    public decimal? TotalOperacionGs { get; set; }

    [XmlElement("dTotOpeGs")]
    public string TotalOperacionGsStr
    {
        get => TotalOperacionGs?.ToString("F8", CultureInfo.InvariantCulture);
        set => TotalOperacionGs = decimal.Parse(value, CultureInfo.InvariantCulture);
    }

    public bool ShouldSerializeTotalOperacionGsStr() => TotalOperacionGs.HasValue;

    public GValorRestaItem(){}

}

// Campos que describen el IVA de la operación por ítem (E730-E739)
public class GCamIVA // Nodo Padre E730
{
    [XmlElement("iAfecIVA")]
    public int AfectacionIVA { get; set; }

    [XmlElement("dDesAfecIVA")]
    public string DescripcionAfectacionIVA {get; set; }

    [XmlElement("dPropIVA")]
    public int ProporcionIVA { get; set; }

    [XmlElement("dTasaIVA")]
    public int TasaIVA { get; set; }

    [XmlIgnore]
    public decimal BaseGravadaIVA { get; set; }

    [XmlElement("dBasGravIVA")]
    public string BaseGravadaIVAString
    {
        get => BaseGravadaIVA.ToString("F8", CultureInfo.InvariantCulture);
        set => BaseGravadaIVA = decimal.Parse(value, CultureInfo.InvariantCulture);
    }

    [XmlIgnore]
    public decimal LiquidacionIVA { get; set; }

    [XmlElement("dLiqIVAItem")]
    public string LiquidacionIVAString
    {
        get => LiquidacionIVA.ToString("F8", CultureInfo.InvariantCulture);
        set => LiquidacionIVA = decimal.Parse(value, CultureInfo.InvariantCulture);
    }

    [XmlIgnore]
    public decimal BaseExenta { get; set; }

    [XmlElement("dBasExe")]
    public string BaseExentaString
    {
        get => BaseExenta.ToString("F8", CultureInfo.InvariantCulture);
        set => BaseExenta = decimal.Parse(value, CultureInfo.InvariantCulture);
    }

    public GCamIVA(){}
}