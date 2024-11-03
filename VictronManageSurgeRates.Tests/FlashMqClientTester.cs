using BigMission.TestHelpers.Delay;
using Microsoft.Extensions.Logging;
using MQTTnet.Client;

namespace VictronManageSurgeRates.Tests;

internal class FlashMqClientTester : FlashMqClient
{
    public FlashMqClientTester(IMqttClient mqtt, ILoggerFactory loggerFactory, IAsyncDelay asyncDelay) : base(mqtt, loggerFactory, asyncDelay) { }

    public async Task ExecuteAsyncTest(CancellationToken stoppingToken)
    {
        await base.ExecuteAsync(stoppingToken);
    }

    public void SetPublishCompleted(bool value)
    {
        publishCompleted = value;
    }

    public void SetSoc(double? value)
    {
        Soc = value;
    }
    public void SetGeneratorState(GeneratorState? state)
    {
        GeneratorState = state;
    }
    public void SetInverterMode(InverterMode? mode)
    {
        InverterMode = mode;
    }
}
