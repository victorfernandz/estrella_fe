using System;
using System.IO;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

class Program
{
    static void Main(string[] args)
    {
        try
        {
            Config config = Config.LoadConfig();

            ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;

            var host = Host.CreateDefaultBuilder(args)
                .UseWindowsService()
                .ConfigureServices(services =>
                {
                    services.AddSingleton(config);
                    services.AddHostedService<MultiBaseSAPCDCService>();
                    services.AddHostedService<EmailQueueService>();

                    services.AddSingleton<EmpresaService>();
                    services.AddSingleton<FacturaService>();
                    services.AddSingleton<NotaCreditoService>();
                    services.AddSingleton<NotaRemisionService>();

                    services.AddSingleton<LoggerSifenService>(sp =>
                    {
                        var cfg = sp.GetRequiredService<Config>();
                        var logger = sp.GetRequiredService<ILogger<LoggerSifenService>>();
                        string connectionString = cfg.GetHanaConnectionString();
                        return new LoggerSifenService(connectionString, logger);
                    });

                    services.AddSingleton<EmailQueueRepository>(sp =>
                    {
                        var cfg = sp.GetRequiredService<Config>();
                        var logger = sp.GetRequiredService<ILogger<EmailQueueRepository>>();
                        return new EmailQueueRepository(cfg.GetHanaConnectionString(), logger);
                    });

                    services.AddLogging(configure =>
                    {
                        configure.AddConsole();
                        string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                        Directory.CreateDirectory(logDir);
                        configure.AddFile(Path.Combine(logDir, "sifen-service-{Date}.log"));
                    });
                })
                .Build();

            host.Run();
        }
        catch (Exception ex)
        {
            File.WriteAllText("error.log", ex.ToString());
            Console.WriteLine($"Error al iniciar el servicio: {ex.Message}");
        }
    }
}
