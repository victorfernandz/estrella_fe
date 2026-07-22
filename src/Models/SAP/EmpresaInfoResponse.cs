//Respuesta del SL para los datos de la Empresa desde PLPY
public class PlpyResponse
{
    public List<PlpyRecord> value { get; set; }
}

public class PlpyRecord
{
    public string Code { get; set; }
    public string Name { get; set; }
    public List<DempRecord> EPY_DEMPCollection { get; set; }
}

public class DempRecord
{
    public string U_NEMP { get; set; }
    public string U_RUCE { get; set; }
    public int U_DVEMI { get; set; }
    public int U_TIPCONT { get; set;}
    public string U_DSUC { get; set; }
    public int U_NUMCASA { get; set; }
    public int U_DEPT { get; set; }
    public int U_DIST { get; set; }
    public int U_BALO { get; set; }
    public string U_PHONE { get; set; }
    public string U_EMAIL { get; set; }
}

public class DptoResponse {
    public List<DptoRecord> value { get; set; }
}

public class DptoRecord {
    public string Code { get; set; }
    public string U_NDEP { get; set; }
}

public class DistResponse {
    public List<DistRecord> value { get; set; }
}

public class DistRecord {
    public string Code { get; set; }
    public string U_NCIU { get; set; }
}

public class BaloResponse {
    public List<BaloRecord> value { get; set; }
}

public class BaloRecord {
    public string Code { get; set; }
    public string U_NLOC { get; set; }
}

// Respuesta que contiene las obligaciones
public class ObligacionesResponse 
{
    public List<ObligacionesRecord> value { get; set; }
}

public class ObligacionesRecord
{
    public string Code { get; set; }
    public List<Obligaciones> EPY_OBLICollection { get; set; }
}

public class Obligaciones
{
    public int U_COBLI; // Código de las obligaciones
    public string U_NOBLI; // Descripción de las obligaciones
}

// Respuesta que contiene una lista de actividades económicas
public class ActividadEconomicaResponse 
{
    public List<ActividadEconomicaRecord> value { get; set; }
}

public class ActividadEconomicaRecord
{
    public string Code { get; set; }
    public List<ActividadEconomicaDetalle> EPY_ACEGRACollection { get; set; }
}

public class ActividadEconomicaDetalle 
{
    public string U_CACEG { get; set; }  // Código de actividad económica
    public string U_DACEG { get; set; }  // Descripción de actividad económica
}
