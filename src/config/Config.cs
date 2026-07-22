// Archivo: Config.cs
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

public class Config
{
    public List<SapServiceLayerConfig> SapServiceLayerList { get; set; }
    public HanaDatabaseConfig HanaDatabase { get; set; }

    public static Config LoadConfig()
    {
        try
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string configPath = Path.Combine(basePath, "config", "config.json");

            if (!File.Exists(configPath))
            {
                throw new FileNotFoundException($"Archivo de configuración no encontrado: {configPath}");
            }

            string json = File.ReadAllText(configPath);
            var config = JsonConvert.DeserializeObject<Config>(json);

            if (config.SapServiceLayerList == null || config.SapServiceLayerList.Count == 0)
            {
                throw new InvalidOperationException("Configuración de bases SAP no encontrada");
            }

            return config;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al cargar la configuración: {ex.Message}");
            throw;
        }
    }

    public string GetHanaConnectionString()
    {
        if (HanaDatabase == null)
        {
            throw new InvalidOperationException("La configuración de la base de datos HANA no está disponible");
        }

        return $"Driver=HDBODBC;ServerNode={HanaDatabase.ServerNode};UID={HanaDatabase.UserName};PWD={HanaDatabase.Password};";
    }
}

public class SapServiceLayerConfig
{
    public string Url { get; set; }
    public string CompanyDB { get; set; }
    public string UserName { get; set; }
    public string Password { get; set; }
    public SifenConfig Sifen { get; set; }
}

public class HanaDatabaseConfig
{
    public string ServerNode { get; set; }
    public string UserName { get; set; }
    public string Password { get; set; }
    public string Schema { get; set; }
    public string OdbcDsn { get; set; }   // System DSN 32-bit compartido para generar PDFs (ej: "HANA_FE")
}

public class SifenConfig
{
    public string Url { get; set; }
    public string IdCSC { get; set; }
    public string CSC { get; set; }
}

