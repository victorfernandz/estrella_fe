//Datos de la empresa
public class EmpresaInfo
{
    public string NombreEmpresa { get; set; }
    public string Ruc { get; set; }
    public int Dv { get; set; }
    public int TipoContribuyente { get; set;}
    public string DireccionEmisor { get; set; }
    public int NumeroCasaEmisor { get; set; }
    public int CodDepartamento { get; set; }
    public string DescDepartamento { get; set; }
    public int CodDistrito { get; set; }
    public string DescDistrito { get; set; }
    public int CodLocalidad { get; set; }
    public string DescLocalidad { get; set; }
    public string TelefEmisor { get; set; }
    public string EmailEmisor { get; set; }
    
    // Lista de actividades económicas
    public List<ActividadEconomica> ActividadesEconomicas { get; set; } = new List<ActividadEconomica>();

    public List<ObligacionAfectada> ObligacionesAfectadas { get; set; } = new List<ObligacionAfectada>();
}

// Clase para cada actividad económica
public class ActividadEconomica
{
    public string Codigo { get; set; }
    public string Descripcion { get; set; }
}

// Modelo para las obligaciones afectadas
public class ObligacionAfectada
{
    public int Codigo { get; set; }      // U_COBLI - Código de obligación
    public string Descripcion { get; set; } // U_NOBLI - Descripción de obligación
}