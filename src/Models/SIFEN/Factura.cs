using System.Diagnostics.Contracts;

public class Factura
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
    public string iTipTra { get; set; }
    public int iIndPres { get; set; }
    public int iCondOpe { get; set; }
    public int iCondCred { get; set; }
    public decimal dTiCam { get; set; }
    public string Comments { get; set; }
    public BusinessPartner BusinessPartner { get; set; }
    public Currencies Currencies { get; set; }
    public GPagCred? OperacionCredito { get; set; }
    public List<Item> Items { get; set; } = new List<Item>();
    public GPaConEIni? PagoContado { get; set; }
}
