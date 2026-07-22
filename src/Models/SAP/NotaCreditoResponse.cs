public class NotaCreditoResponse
{
    public CreditNotesData CreditNotes { get; set; }
    public DocumentLineData DocumentLines { get; set; }
    public BusinessPartnerData BusinessPartners { get; set; }
    public CurrenciesData Currencies { get; set; }
}

public class CreditNotesData
{
    public int DocEntry { get; set; }
    public string DocType { get; set; }
    public string U_FE_CDC { get; set; }
    public string U_FE_Estado { get; set; }
    public string U_FE_CODERR { get; set; }
    public string U_CENT_TIPO_DOC { get; set; }
    public string CardCode { get; set; }
    public string U_CENT_EST { get; set; }
    public string U_CENT_PE { get; set; }
    public string FolioNumber { get; set; }
    public string DocDate { get; set; }
    public int DocTime { get; set; }
    public string U_FITE { get; set; }
    public int U_CENT_TIMB { get; set; }
    public decimal DocRate { get; set; }
    public string U_NUMFC { get; set; }
    public int U_TIMFC { get; set; }
    public int U_DASO { get; set; }
    public int U_FE_MotEmision { get; set; }
    public string Comments { get; set; }
}