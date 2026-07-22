using Newtonsoft.Json;

public class FacturaResponse
{
    public InvoiceData Invoices { get; set; }
    public DocumentLineData DocumentLines { get; set; }
    public BusinessPartnerData BusinessPartners { get; set; }
    public CurrenciesData Currencies { get; set; }
}

public class InvoiceData
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
    public string FolioNumber { get; set; }
    public string DocDate { get; set; }
    public string U_FITE { get; set; }
    public int U_TIM { get; set; }
    public string U_EXX_FE_TipoTran { get; set; }
    public int U_EXX_FE_IndPresencia { get; set; }
    public int PaymentGroupCode { get; set; }
    public int NumberOfInstallments { get; set; }
    public decimal DocRate { get; set; }
    public string Comments { get; set; }
}

public class DocumentLineData
{
    public string ItemCode { get; set; }
    public string ItemDescription { get; set; }
    public string ItemDetails { get; set; }
    public decimal Quantity { get; set; }
    public decimal PriceAfterVAT { get; set; }
    public decimal Rate { get; set; }
    public string TaxCode { get; set; }
    public decimal TaxPercentagePerRow { get; set; }
    public string WarehouseCode { get; set; } // Usado para resolver el local de salida (gCamSal) de la Nota de Remisión
}

public class BusinessPartnerData
{
    public string CardCode { get; set; }    
    public string CardName { get; set; }
    public string FederalTaxID { get; set; } 
    public string EmailAddress { get; set; }
    public string Phone1 { get; set; }
    public string Cellular { get; set;}
    public int? U_TIPCONT { get; set; } // Tipo Contribuyente
    public string U_CRSI { get; set; }  // Naturaleza del SN
    public string U_CRID { get; set; } // Tipo identificación
    public int U_EXX_FE_TipoOperacion { get; set; }

    public List<BPAddressInfo> BPAddresses { get; set; }

    public class BPAddressInfo
    {
        public string CardCode { get; set; } 
        public string Country { get; set; }
        public string Street { get; set; }
        public int? StreetNo { get; set; }
        public int? U_EXX_FE_DEPT { get; set; }
        public int? U_EXX_FE_DIST { get; set; }
        public int? U_EXX_FE_BALO { get; set; }
    }
}

public class CurrenciesData
{
    public string DocumentsCode { get; set; }
    public string Name { get; set; }
}

// Clase para los detalles de una dirección
public class BPAddressDetalle
{
    public string AddressName { get; set; }
    public string Street { get; set; }
    public string Block { get; set; }
    public string ZipCode { get; set; }
    public string City { get; set; }
    public string County { get; set; }
    public string Country { get; set; }
    public string State { get; set; }
    public string AddressType { get; set; }
    public int? StreetNo { get; set; }
    public string BPCode { get; set; }
    public int? RowNum { get; set; }
    public int? U_EXX_FE_DEPT { get; set; }
    public int? U_EXX_FE_DIST { get; set; }
    public int? U_EXX_FE_BALO { get; set; }
    public string U_EXX_FE_BARR { get; set; }
}

// Clase auxiliar para deserializar la respuesta de direcciones
public class BPAddressesWrapper
{
    [JsonProperty("BPAddresses")]
    public List<BPAddressDetalle> BPAddresses { get; set; }
}

public class CuotaResponse
{
    public decimal Total { get; set; }
    public decimal TotalFC { get; set; }
    public string U_FECHAV { get; set; }
    public int InstallmentId { get; set; }
}

public class IncomingPaymentResponse
{
    [JsonProperty("DocEntry")]
    public int DocEntry { get; set; }

    [JsonProperty("TransferSum")]
    public decimal TransferSum { get; set; }

    [JsonProperty("CashSum")]
    public decimal CashSum { get; set; }

    [JsonProperty("PaymentInvoices")]
    public List<PaymentInvoiceItem> PaymentInvoices { get; set; }
}

public class PaymentInvoiceItem
{
    [JsonProperty("DocEntry")]
    public int DocEntry { get; set; }

    [JsonProperty("SumApplied")]
    public decimal SumApplied { get; set; }

    [JsonProperty("AppliedFC")]
    public decimal AppliedFC { get; set; }

    [JsonProperty("AppliedSys")]
    public decimal AppliedSys { get; set; }
}
