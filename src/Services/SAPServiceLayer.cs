using System;
using System.Net;
using System.Text;
using Newtonsoft.Json;

public class SAPServiceLayer
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _companyDB;
    private readonly string _username;
    private readonly string _password;
    private string _sessionId;
    private string _routeId;

    public SAPServiceLayer(SapServiceLayerConfig config)
    {
        _baseUrl = config.Url.TrimEnd('/') + "/";
        _companyDB = config.CompanyDB;
        _username = config.UserName;
        _password = config.Password;

        // Para ignorar SSL
        ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;

        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
        };

        _httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(_baseUrl),
            Timeout = TimeSpan.FromMinutes(1)
        };

        _httpClient.DefaultRequestHeaders.Add("Prefer", "odata.maxpagesize=100");
    }

    public async Task<bool> Login()
    {
        var requestBody = new
        {
            CompanyDB = _companyDB,
            UserName = _username,
            Password = _password
        };

        var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("Login", content);

        if (response.IsSuccessStatusCode)
        {
            var jsonResponse = await response.Content.ReadAsStringAsync();
            dynamic loginData = JsonConvert.DeserializeObject(jsonResponse);
            _sessionId = loginData.SessionId;
            _routeId = loginData.RouteId;

            _httpClient.DefaultRequestHeaders.Add("B1SESSION", _sessionId);
            _httpClient.DefaultRequestHeaders.Add("RouteID", _routeId);

            return true;
        }
        return false;
    }

    public async Task Logout()
    {
        if (string.IsNullOrEmpty(_sessionId))
            return;

        try
        {
            var response = await _httpClient.PostAsync("Logout", null);
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("Sesión cerrada correctamente en SAP.");
            }
            else
            {
                Console.WriteLine($"Error al cerrar sesión en SAP: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Excepción en Logout: {ex.Message}");
        }
        finally
        {
            _sessionId = null;
            _routeId = null;
            
            _httpClient.DefaultRequestHeaders.Remove("B1SESSION");
            _httpClient.DefaultRequestHeaders.Remove("RouteID");
        }
    }

    public HttpClient GetHttpClient()
    {
        return _httpClient;
    }
}
