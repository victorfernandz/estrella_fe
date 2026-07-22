// Archivo: MultiBaseSAPCDCService.cs
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public class MultiBaseSAPCDCService : BackgroundService
{
    private readonly ILogger<MultiBaseSAPCDCService> _logger;
    private readonly Config _config;
    private readonly IServiceProvider _serviceProvider;
    private readonly EmailQueueRepository _emailQueue;

    public MultiBaseSAPCDCService(ILogger<MultiBaseSAPCDCService> logger, Config config, IServiceProvider serviceProvider, EmailQueueRepository emailQueue)
    {
        _logger = logger;
        _config = config;
        _serviceProvider = serviceProvider;
        _emailQueue = emailQueue;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MultiBaseSAPCDCService iniciado...");

        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var sapConfig in _config.SapServiceLayerList)
            {
                try
                {
                    _logger.LogInformation($"Procesando base de datos: {sapConfig.CompanyDB}");

                    using var scope = _serviceProvider.CreateScope();
                    var scopedServices = scope.ServiceProvider;

                    var sapService = new SAPServiceLayer(sapConfig);
                    var empresaService = new EmpresaService(sapService, scopedServices.GetRequiredService<ILogger<EmpresaService>>());

                    var eventoCancelacion = new EventoServiceCancelacion(sapService, scopedServices.GetRequiredService<ILogger<EventoServiceCancelacion>>());
                    var eventoInutilizacion = new EventoService(sapService, scopedServices.GetRequiredService<ILogger<EventoService>>());
                    
                    var facturaService = new FacturaService(sapService, scopedServices.GetRequiredService<ILogger<FacturaService>>());
                    var notaCreditoService = new NotaCreditoService(sapService, scopedServices.GetRequiredService<ILogger<NotaCreditoService>>());
                    var notaRemisionService = new NotaRemisionService(sapService, scopedServices.GetRequiredService<ILogger<NotaRemisionService>>());
                    var loggerSifen = scopedServices.GetRequiredService<LoggerSifenService>();

                    var envioService = new EnvioSifenService(sapConfig.Sifen.Url, loggerSifen, new Config { SapServiceLayerList = new List<SapServiceLayerConfig> { sapConfig }, HanaDatabase = _config.HanaDatabase },
                        scopedServices.GetRequiredService<ILogger<EnvioSifenService>>(), sapService, _emailQueue
                    );

                    var servicio = new SAPCDCService(scopedServices.GetRequiredService<ILogger<SAPCDCService>>(), sapService, facturaService, notaCreditoService, notaRemisionService, envioService, eventoCancelacion, empresaService, loggerSifen, eventoInutilizacion,
                        new Config { SapServiceLayerList = new List<SapServiceLayerConfig> { sapConfig }, HanaDatabase = _config.HanaDatabase }
                    );

                    await servicio.ProcesarTodoAsync(stoppingToken);

                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error procesando base {sapConfig.CompanyDB}");
                }
            }

            _logger.LogInformation("Esperando 5 minutos para el siguiente ciclo...");
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
} 
