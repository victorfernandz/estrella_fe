public class Item
{
    public string dCodInt { get; set; }
    public string dDesProSer { get; set; }
    public decimal dCantProSer { get; set; }
    public decimal dPUniProSer { get; set; }
    public int cUniMed { get; set; }
    public string dDesUniMed { get; set; }
    public decimal dTiCamIt { get; set; }
    public decimal dTotBruOpeItem { get; set; }
    public string taxCode { get; set; }
    public int iAfecIVA { get; set; }
    public string dDesAfecIVA { get; set; }
    public int dPropIVA { get; set; }
    public decimal dTasaIVA { get; set; }
    public decimal dBasGravIVA { get; set; }
    public decimal dLiqIVAItem { get; set; }
    public decimal dBasExe { get; set; }

    // Campos para descuentos y anticipos

    public decimal dDescItem { get; set; } = 0;
    public decimal dPorcDesIt { get; set; } = 0;
    public decimal dDescGloItem { get; set; } = 0;
    public decimal dAntPreUniIt { get; set; } = 0;
    public decimal dAntGloPreUniIt { get; set; } = 0;

    // Campos para Nota de Remisión (E715-E718): tolerancia de quiebra/merma según RG 41/14
    public int? cRelMerc { get; set; }
    public decimal? dCanQuiMer { get; set; }
    public decimal? dPorQuiMer { get; set; }
}