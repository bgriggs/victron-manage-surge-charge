using BigMission.TestHelpers;
using BigMission.TestHelpers.Delay;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace VictronManageSurgeRates.Tests;

internal class ApplicationTester : Application
{
    public ApplicationTester(IConfiguration configuration, ILoggerFactory loggerFactory, IFlashMqClient flashMqClient, IAsyncDelay asyncDelay, IDateTimeHelper dateTime) :
        base(configuration, loggerFactory, flashMqClient, asyncDelay, dateTime) { }

    public async Task ExecuteAsyncTest(CancellationToken stoppingToken)
    {
        await base.ExecuteAsync(stoppingToken);
    }

    public void SetInverterModeOverridden(bool state)
    {
        inverterModeOverridden = state;
    }

    public bool GetInverterModeOverridden()
    {
        return inverterModeOverridden;
    }
}