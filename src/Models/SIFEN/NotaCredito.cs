public class NotaCredito
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
    public int U_TIM { get; set; }
    public string U_FITE { get; set; }
    public decimal dTiCam { get; set; }
    public int iMotEmi { get; set; }
    public int iTipDocAso { get; set; }
    public string dCdCDERef { get; set; }
    public int dNTimDI { get; set; } 
    public string dEstDocAso { get; set; }
    public string dPExpDocAso { get; set; }    
    public string dNumDocAso { get; set; }
    public int iTipoDocAso { get; set; }
    public string dDTipoDocAso { get; set; }
    public int? timbradoSAP { get; set; }
    public DateTime dFecEmiDI { get; set; }
    public string U_NUMFC { get; set; }
    public string Comments { get; set; }
    public BusinessPartner BusinessPartner { get; set; }
    public Currencies Currencies { get; set; }
    public List<Item> Items { get; set; } = new List<Item>();

}
