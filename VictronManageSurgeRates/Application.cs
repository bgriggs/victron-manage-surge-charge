using BigMission.TestHelpers;
using BigMission.TestHelpers.Delay;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace VictronManageSurgeRates;

public class Application : BackgroundService
{
    private readonly IConfiguration configuration;
    private readonly IFlashMqClient flashMqClient;
    private readonly IAsyncDelay asyncDelay;

    private ILogger Logger { get; }
    public IDateTimeHelper DateTime { get; }
    /// <summary>
    /// Whether this application set the inverter's mode.
    /// </summary>
    protected bool inverterModeOverridden = false;

    public Application(IConfiguration configuration, ILoggerFactory loggerFactory, IFlashMqClient flashMqClient, IAsyncDelay asyncDelay, IDateTimeHelper dateTime)
    {
        this.configuration = configuration;
        this.flashMqClient = flashMqClient;
        this.asyncDelay = asyncDelay;
        DateTime = dateTime;
        Logger = loggerFactory.CreateLogger(GetType().Name);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Logger.LogInformation("Starting Application ExecuteAsync...");

        var ip = configuration["CerboIP"] ?? throw new ArgumentNullException("configuration[CerboIP]");
        var deviceId = configuration["DeviceID"] ?? throw new ArgumentNullException("configuration[DeviceID]");
        var startStr = configuration["TODStart"] ?? throw new ArgumentNullException("configuration[TODStart]");
        var endStr = configuration["TODEnd"] ?? throw new ArgumentNullException("configuration[TODEnd]");
        var start = System.DateTime.ParseExact(startStr, "HH:mm", CultureInfo.InvariantCulture);
        var end = System.DateTime.ParseExact(endStr, "HH:mm", CultureInfo.InvariantCulture);
        var minSoc = int.Parse(configuration["MinSOC"] ?? throw new ArgumentNullException("configuration[MinSOC]"));

        try
        {
            // Initialize the FlashMQ client and connect to the MQTT server
            await flashMqClient.Connect(ip, deviceId, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Check for generator running
                    if (!flashMqClient.GeneratorState.HasValue || 
                        (flashMqClient.GeneratorState.HasValue && flashMqClient.GeneratorState.Value != GeneratorState.Stopped))
                    {
                        Logger.LogInformation($"Generator is not stopped (state={flashMqClient.GeneratorState}), not executing, waiting...");

                        // When generator is running but inverter is not on and charging, go ahead and set it to On
                        if (flashMqClient.GeneratorState.HasValue && flashMqClient.GeneratorState.Value == GeneratorState.Running && 
                            flashMqClient.InverterMode != InverterMode.On)
                        {
                            Logger.LogInformation("Generator is running, setting inverter mode to On...");
                            await flashMqClient.OverrideInverterMode(InverterMode.On, stoppingToken);
                        }
                        await asyncDelay.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                        continue;
                    }
                    
                    // Check to see if the SOC is available and above the threshold
                    if (!flashMqClient.Soc.HasValue)
                    {
                        Logger.LogInformation("SOC is not available, waiting...");
                        await asyncDelay.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                        continue;
                    }
                    if (flashMqClient.Soc.HasValue && flashMqClient.Soc.Value < minSoc)
                    {
                        Logger.LogInformation($"SOC is below threshold {flashMqClient.Soc.Value} < {minSoc}.");
                        Logger.LogDebug($"Checking inverter mode ({flashMqClient.InverterMode}) and whether it was overridden by this application...");
                        if (flashMqClient.InverterMode == InverterMode.InverterOnly && inverterModeOverridden)
                        {
                            Logger.LogInformation($"Setting inverter mode back to On to avoid dropping below SOC threshold of {minSoc}.");
                            await flashMqClient.OverrideInverterMode(InverterMode.On, stoppingToken);
                            inverterModeOverridden = false;

                            // We're going to give up for a while so we don't bounce back and forth on the SOC threshold
                            Logger.LogWarning("Giving up for 30 minutes...");
                            await asyncDelay.Delay(TimeSpan.FromMinutes(30), stoppingToken);
                            continue;
                        }

                        await asyncDelay.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                        continue;
                    }

                    // See if TOD is in range
                    var now = DateTime.Now;
                    if (now.TimeOfDay >= start.TimeOfDay && now.TimeOfDay <= end.TimeOfDay)
                    {
                        // Check inverter state
                        Logger.LogDebug($"Checking inverter mode: {flashMqClient.InverterMode}. Must be On to continue.");
                        if (flashMqClient.InverterMode == InverterMode.On)
                        {
                            Logger.LogInformation("Inverter mode is On, setting to InverterOnly");
                            await flashMqClient.OverrideInverterMode(InverterMode.InverterOnly, stoppingToken);
                            inverterModeOverridden = true;
                        }
                        else
                        {
                            Logger.LogInformation("Inverter mode is not On and will not be changed");
                        }
                    }
                    else // Out of time range
                    {
                        Logger.LogDebug($"Not in the time range {startStr}-{endStr}.");

                        // Check inverter state to turn back on
                        Logger.LogDebug($"Checking whether the inverter mode is overridden ({inverterModeOverridden}) and the inverter mode ({flashMqClient.InverterMode}). Inverter mode must be overridden and mode must be InverterOnly to continue with resetting to ON.");
                        if (inverterModeOverridden && flashMqClient.InverterMode == InverterMode.InverterOnly)
                        {
                            Logger.LogInformation("Inverter mode is InverterOnly, setting to On");
                            await flashMqClient.OverrideInverterMode(InverterMode.On, stoppingToken);
                            inverterModeOverridden = false;
                        }
                        else
                        {
                            Logger.LogInformation($"TOD out of range--no change--inverter mode ({flashMqClient.InverterMode}) is not overridden.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error in application");
                }
                await asyncDelay.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in application initialization connection");
        }
    }
}
