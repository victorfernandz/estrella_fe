using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

public class EmpresaService
{
    private readonly SAPServiceLayer _sapServiceLayer;
    private readonly HttpClient _httpClient;
    private readonly ILogger<EmpresaService> _logger;
    
    public EmpresaService(SAPServiceLayer sapServiceLayer, ILogger<EmpresaService> logger)
    {
        _sapServiceLayer = sapServiceLayer;
        _httpClient = sapServiceLayer.GetHttpClient();
        _logger = logger;
    }

    public async Task<EmpresaInfo> GetEmpresaInfo()
    {
        string query = "EPY_PLPY?$select=Code,EPY_DEMPCollection";
        var jsonResponse = await HttpHelper.GetStringAsync(_httpClient, query, _logger, "Error en la consulta a SAP EPY_PLPY");
        
        if (string.IsNullOrEmpty(jsonResponse))
        {
            return null;
        }
        
        var rawJson = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonResponse);
        if (rawJson == null || !rawJson.ContainsKey("value"))
        {
            _logger.LogWarning("No se encontraron datos en la respuesta de EPY_PLPY.");
            return null;
        }

        // Deserializar la respuesta
        var plpyResponse = JsonConvert.DeserializeObject<PlpyResponse>(jsonResponse);
        
        if (plpyResponse?.value == null || !plpyResponse.value.Any())
        {
            _logger.LogWarning("No se encontraron registros en EPY_PLPY.");
            return null;
        }

        // Obtenemos el primer registro
        var primerRegistro = plpyResponse.value.First();
        
        // Buscamos los datos en EPY_DEMP
        if (primerRegistro.EPY_DEMPCollection == null || !primerRegistro.EPY_DEMPCollection.Any())
        {
            _logger.LogWarning("No se encontraron datos en EPY_DEMP.");
            return null;
        }

        var datosEmpresa = primerRegistro.EPY_DEMPCollection.First();
        
        // Se crea la instancia de EmpresaInfo
        var empresaInfo = new EmpresaInfo
        {
            NombreEmpresa = datosEmpresa.U_NEMP,
            Ruc = datosEmpresa.U_RUCE,
            Dv = datosEmpresa.U_DVEMI,
            TipoContribuyente = datosEmpresa.U_TIPCONT,
            DireccionEmisor = datosEmpresa.U_DSUC,
            NumeroCasaEmisor = datosEmpresa.U_NUMCASA,
            CodDepartamento = datosEmpresa.U_DEPT,
            CodDistrito = datosEmpresa.U_DIST,
            CodLocalidad = datosEmpresa.U_BALO,
            TelefEmisor = datosEmpresa.U_PHONE,
            EmailEmisor = datosEmpresa.U_EMAIL,
        };

        // Obtener las descripciones geográficas
        await ObtenerDescripcionesGeograficas(empresaInfo, datosEmpresa);

        return empresaInfo;
    }

    private async Task ObtenerDescripcionesGeograficas(EmpresaInfo empresaInfo, dynamic datosEmpresa)
    {
        try
        {
            // Convertir explícitamente a string para evitar errores de tipos
            string deptCode = Convert.ToString(datosEmpresa.U_DEPT);
            string distCode = Convert.ToString(datosEmpresa.U_DIST);
            string baloCode = Convert.ToString(datosEmpresa.U_BALO);

            // Obtener descripción del departamento
            empresaInfo.DescDepartamento = await ObtenerDescripcionGeografica("EPY_DPTO", deptCode, "U_NDEP", "departamento");

            // Obtener descripción del distrito
            empresaInfo.DescDistrito = await ObtenerDescripcionGeografica("EPY_DIST", distCode, "U_NCIU", "distrito");

            // Obtener descripción de la localidad
            empresaInfo.DescLocalidad = await ObtenerDescripcionGeografica("EPY_BALO", baloCode, "U_NLOC", "localidad");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error al obtener descripciones geográficas: {ex.Message}");
        }
    }

    private async Task<string> ObtenerDescripcionGeografica(string entidad, string codigo, string campoDescripcion, string tipo)
    {
        if (string.IsNullOrEmpty(codigo))
        {
            _logger.LogWarning($"Código de {tipo} no especificado.");
            return "";
        }

        string query = $"{entidad}?$select=Code,{campoDescripcion}&$filter=Code eq '{codigo}'";
        
        var jsonResponse = await HttpHelper.GetStringAsync(_httpClient, query, _logger, $"Error al consultar {tipo}");
        
        if (string.IsNullOrEmpty(jsonResponse))
        {
            return "";
        }

        try
        {
            dynamic respuesta = JsonConvert.DeserializeObject(jsonResponse);
            
            if (respuesta?.value != null && respuesta.value.Count > 0)
            {
                return Convert.ToString(respuesta.value[0][campoDescripcion]);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error al procesar la respuesta de {tipo}: {ex.Message}");
        }
        
        return "";
    }

    public async Task<List<ActividadEconomica>> GetActividadesEconomicas()
    {
        try
        {
            string query = "EPY_ACG?$select=Code,EPY_ACEGRACollection";
            var jsonResponse = await HttpHelper.GetStringAsync(_httpClient, query, _logger, "Error en la consulta a SAP EPY_ACG");
            
            if (string.IsNullOrEmpty(jsonResponse))
            {
                return new List<ActividadEconomica>();
            }
            
            dynamic responseObj = JsonConvert.DeserializeObject(jsonResponse);
            var actividades = new List<ActividadEconomica>();
            
            if (responseObj?.value == null || responseObj.value.Count == 0)
            {
                _logger.LogWarning("No se encontraron registros de actividades económicas.");
                return actividades;
            }

            // Procesar todas las actividades económicas de todos los registros
            foreach (var registro in responseObj.value)
            {
                if (registro.EPY_ACEGRACollection != null && registro.EPY_ACEGRACollection.Count > 0)
                {
                    foreach (var actividad in registro.EPY_ACEGRACollection)
                    {
                        actividades.Add(new ActividadEconomica
                        {
                            Codigo = Convert.ToString(actividad.U_CACT),
                            Descripcion = Convert.ToString(actividad.U_NACTECO)
                        });
                    }
                }
            }

            return actividades;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error al obtener actividades económicas: {ex.Message}");
            return new List<ActividadEconomica>();
        }
    }

    public async Task<List<ObligacionAfectada>> GetObligacionesAfectadas()
    {
        try
        {
            string query = "EPY_OCG?$select=Code,EPY_OBLICollection";
            var jsonResponse = await HttpHelper.GetStringAsync(_httpClient, query, _logger, "Error en la consulta a SAP EPY_OCG");
            
            if (string.IsNullOrEmpty(jsonResponse))
            {
                return new List<ObligacionAfectada>();
            }
            
            var obligacionesResponse = JsonConvert.DeserializeObject<ObligacionesResponse>(jsonResponse);
            var obligaciones = new List<ObligacionAfectada>();
            
            if (obligacionesResponse?.value == null || !obligacionesResponse.value.Any())
            {
                _logger.LogWarning("No se encontraron registros de obligaciones afectadas.");
                return obligaciones;
            }

            // Procesar todas las obligaciones
            foreach (var registro in obligacionesResponse.value)
            {
                if (registro.EPY_OBLICollection != null && registro.EPY_OBLICollection.Any())
                {
                    // Agregar cada obligación a la lista
                    foreach (var obligacion in registro.EPY_OBLICollection)
                    {
                        obligaciones.Add(new ObligacionAfectada 
                        { 
                            Codigo = obligacion.U_COBLI,
                            Descripcion = obligacion.U_NOBLI
                        });
                    }
                }
            }

            return obligaciones;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error al obtener obligaciones afectadas: {ex.Message}");
            return new List<ObligacionAfectada>();
        }
    }
}