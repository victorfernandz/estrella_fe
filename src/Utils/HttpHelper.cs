using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

public static class HttpHelper
{
    /// Ejecuta una solicitud HTTP GET y procesa la respuesta
    public static async Task<T> GetAsync<T>(HttpClient httpClient, string query, ILogger logger, string errorMessage, T defaultValue = default)
    {
        try
        {
            var response = await httpClient.GetAsync(query);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                logger.LogError($"{errorMessage}: {response.StatusCode}");
                logger.LogError($"Detalles: {errorContent}");
                return defaultValue;
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<T>(jsonResponse);
        }
        catch (Exception ex)
        {
            logger.LogError($"{errorMessage}: {ex.Message}");
            if (ex.InnerException != null)
            {
                logger.LogError($"Error interno: {ex.InnerException.Message}");
            }
            return defaultValue;
        }
    }

    public static async Task<string> GetStringAsync(HttpClient httpClient, string query, ILogger logger, string errorMessage)
    {
        try
        {
            var response = await httpClient.GetAsync(query);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                logger.LogError($"{errorMessage}: {response.StatusCode}");
                logger.LogError($"Detalles: {errorContent}");
                return null;
            }

            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            logger.LogError($"{errorMessage}: {ex.Message}");
            if (ex.InnerException != null)
            {
                logger.LogError($"Error interno: {ex.InnerException.Message}");
            }
            return null;
        }
    }
}