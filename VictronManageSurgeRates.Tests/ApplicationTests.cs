using BigMission.TestHelpers.Delay;
using BigMission.TestHelpers.Testing;
using Microsoft.Extensions.Configuration;

namespace VictronManageSurgeRates.Tests;

[TestClass]
public class ApplicationTests
{
    private Dictionary<string, string?> configValues = new()
    {
        { "CerboIP", "192.168.1.100" },
        { "DeviceID", "102c6b644fe6" },
        { "MinSOC", "20" },
        { "TODStart", "16:00" },
        { "TODEnd", "20:00" }
    };
    private IConfiguration? configuration;

    [TestInitialize]
    public void Setup()
    {
        configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();
    }

    [TestMethod]
    public async Task Application_GeneratorRunning_Test()
    {
        var asyncDelay = new TestAsyncDelay();
        var loggerFactory = new TestLoggerFactory();
        var flashMqClient = new FlashMqClientTester(new MqttTestClient(), loggerFactory, asyncDelay);
        var dateTime = new TestDateTime();
        var application = new ApplicationTester(configuration!, loggerFactory, flashMqClient, asyncDelay, dateTime);

        flashMqClient.SetGeneratorState(GeneratorState.Running); // <---
        flashMqClient.SetInverterMode(InverterMode.On);
        var tokenSource = new CancellationTokenSource();
        tokenSource.CancelAfter(TimeSpan.FromMilliseconds(300));
        await application.ExecuteAsyncTest(tokenSource.Token);

        // Check the logger to see that it skipped execution
        Assert.IsTrue(loggerFactory.Logger.LogMessages.Last().Contains("Generator is not stopped"));
    }

    [TestMethod]
    public async Task Application_GeneratorRunning_InverterNotOn_Test()
    {
        var asyncDelay = new TestAsyncDelay();
        var loggerFactory = new TestLoggerFactory();
        var flashMqClient = new FlashMqClientTester(new MqttTestClient(), loggerFactory, asyncDelay);
        var dateTime = new TestDateTime();
        var application = new ApplicationTester(configuration!, loggerFactory, flashMqClient, asyncDelay, dateTime);

        flashMqClient.SetGeneratorState(GeneratorState.Running); // <---
        flashMqClient.SetInverterMode(InverterMode.InverterOnly); // <---
        var tokenSource = new CancellationTokenSource();
        tokenSource.CancelAfter(TimeSpan.FromMilliseconds(300));
        await application.ExecuteAsyncTest(tokenSource.Token);

        // When generator is running but inverter is not on and charging, go ahead and set it to On
        Assert.IsTrue(loggerFactory.Logger.LogMessages.Last().Contains("Generator is running, setting inverter mode to On"));
    }

    [TestMethod]
    public async Task Application_GeneratorNoStatus_Test()
    {
        var asyncDelay = new TestAsyncDelay();
        var loggerFactory = new TestLoggerFactory();
        var flashMqClient = new FlashMqClientTester(new MqttTestClient(), loggerFactory, asyncDelay);
        var dateTime = new TestDateTime();
        var application = new ApplicationTester(configuration!, loggerFactory, flashMqClient, asyncDelay, dateTime);

        flashMqClient.SetGeneratorState(null); // <---
        var tokenSource = new CancellationTokenSource();
        tokenSource.CancelAfter(TimeSpan.FromMilliseconds(300));
        await application.ExecuteAsyncTest(tokenSource.Token);

        // Check the logger to see that it skipped execution
        Assert.IsTrue(loggerFactory.Logger.LogMessages.Last().Contains("Generator is not stopped"));
    }

    [TestMethod]
    public async Task Application_SocNull_Test()
    {
        var asyncDelay = new TestAsyncDelay();
        var loggerFactory = new TestLoggerFactory();
        var flashMqClient = new FlashMqClientTester(new MqttTestClient(), loggerFactory, asyncDelay);
        var dateTime = new TestDateTime();
        var application = new ApplicationTester(configuration!, loggerFactory, flashMqClient, asyncDelay, dateTime);

        flashMqClient.SetGeneratorState(GeneratorState.Stopped);
        flashMqClient.SetSoc(null); // <---
        var tokenSource = new CancellationTokenSource();
        tokenSource.CancelAfter(TimeSpan.FromMilliseconds(300));
        await application.ExecuteAsyncTest(tokenSource.Token);

        // Check the logger to see that it skipped execution
        Assert.IsTrue(loggerFactory.Logger.LogMessages.Last().Contains("SOC is not available"));
    }

    [TestMethod]
    public async Task Application_BelowSoc_NoInverterChange_Test()
    {
        // Inverter should not be changed
        var asyncDelay = new TestAsyncDelay();
        var loggerFactory = new TestLoggerFactory();
        var flashMqClient = new FlashMqClientTester(new MqttTestClient(), loggerFactory, asyncDelay);
        var dateTime = new TestDateTime();
        var application = new ApplicationTester(configuration!, loggerFactory, flashMqClient, asyncDelay, dateTime);

        flashMqClient.SetGeneratorState(GeneratorState.Stopped);
        flashMqClient.SetSoc(19.9); // <---
        flashMqClient.SetInverterMode(InverterMode.On); // <--- does not require change
        var tokenSource = new CancellationTokenSource();
        tokenSource.CancelAfter(TimeSpan.FromMilliseconds(300));
        await application.ExecuteAsyncTest(tokenSource.Token);

        // Check the logger to see that it skipped execution
        Assert.IsTrue(loggerFactory.Logger.LogMessages.Last().Contains("Checking inverter mode"));
    }

    [TestMethod]
    public async Task Application_BelowSoc_NoInverterChange_NotOverridden_Test()
    {
        // Inverter should not be changed
        var asyncDelay = new TestAsyncDelay();
        var loggerFactory = new TestLoggerFactory();
        var flashMqClient = new FlashMqClientTester(new MqttTestClient(), loggerFactory, asyncDelay);
        var dateTime = new TestDateTime();
        var application = new ApplicationTester(configuration!, loggerFactory, flashMqClient, asyncDelay, dateTime);

        flashMqClient.SetGeneratorState(GeneratorState.Stopped);
        flashMqClient.SetSoc(19.9); // <---
        flashMqClient.SetInverterMode(InverterMode.InverterOnly);
        application.SetInverterModeOverridden(false); // <--- does not require change to inverter
        var tokenSource = new CancellationTokenSource();
        tokenSource.CancelAfter(TimeSpan.FromMilliseconds(300));
        await application.ExecuteAsyncTest(tokenSource.Token);

        // Check the logger to see that it did not change the inverter
        Assert.IsTrue(loggerFactory.Logger.LogMessages.Last().Contains("Checking inverter mode"));
    }

    [TestMethod]
    public async Task Application_SocFallsBelowThreshold_TurnOn_Test()
    {
        var asyncDelay = new TestAsyncDelay();
        var loggerFactory = new TestLoggerFactory();
        var mqttClient = new MqttTestClient();
        var flashMqClient = new FlashMqClientTester(mqttClient, loggerFactory, asyncDelay);
        var dateTime = new TestDateTime();
        var application = new ApplicationTester(configuration!, loggerFactory, flashMqClient, asyncDelay, dateTime);

        flashMqClient.SetGeneratorState(GeneratorState.Stopped);
        flashMqClient.SetSoc(90); // <---
        flashMqClient.SetInverterMode(InverterMode.On);
        application.SetInverterModeOverridden(false);

        // Turn inverter to inverter only
        dateTime.DateTimeTestValue = DateTime.Parse("2024-01-01 16:00"); // <---
        var tokenSource = new CancellationTokenSource();
        tokenSource.CancelAfter(TimeSpan.FromMilliseconds(300));
        await application.ExecuteAsyncTest(tokenSource.Token);
        flashMqClient.SetInverterMode(InverterMode.InverterOnly);

        Assert.IsTrue(application.GetInverterModeOverridden());

        flashMqClient.SetSoc(19); // <---
        tokenSource = new CancellationTokenSource();
        tokenSource.CancelAfter(TimeSpan.FromMilliseconds(300));
        // Turn inverter to ON
        await application.ExecuteAsyncTest(tokenSource.Token);

        // Check the command to set to inverter only then to ON. (1 is for initial connection keep alive)
        Assert.IsTrue(mqttClient.PublishAsyncCalls > 3);
        Assert.IsFalse(application.GetInverterModeOverridden());
    }

        [TestMethod]
    public async Task Application_BelowSoc_ResetInverter_Test()
    {
        // Inverter should be reset to On
        var asyncDelay = new TestAsyncDelay();
        var loggerFactory = new TestLoggerFactory();
        var mqttClient = new MqttTestClient();
        var flashMqClient = new FlashMqClientTester(mqttClient, loggerFactory, asyncDelay);
        var dateTime = new TestDateTime();
        var application = new ApplicationTester(configuration!, loggerFactory, flashMqClient, asyncDelay, dateTime);

        flashMqClient.SetGeneratorState(GeneratorState.Stopped);
        flashMqClient.SetSoc(19.9); // <---
        flashMqClient.SetInverterMode(InverterMode.InverterOnly);
        application.SetInverterModeOverridden(true); // <--- requires change to inverter
        var tokenSource = new CancellationTokenSource();
        tokenSource.CancelAfter(TimeSpan.FromMilliseconds(300));
        await application.ExecuteAsyncTest(tokenSource.Token);

        // Check the logger to see that the inverter was reset to On
        Assert.IsTrue(mqttClient.PublishAsyncCalls > 0);
    }

    [TestMethod]
    public async Task Application_TODInRange_OverrideInverter_Test()
    {
        // Inverter should be set to Inverter Only
        var asyncDelay = new TestAsyncDelay();
        var loggerFactory = new TestLoggerFactory();
        var mqttClient = new MqttTestClient();
        var flashMqClient = new FlashMqClientTester(mqttClient, loggerFactory, asyncDelay);
        var dateTime = new TestDateTime();
        var application = new ApplicationTester(configuration!, loggerFactory, flashMqClient, asyncDelay, dateTime);

        flashMqClient.SetGeneratorState(GeneratorState.Stopped);
        flashMqClient.SetSoc(90);
        flashMqClient.SetInverterMode(InverterMode.On); // <---
        application.SetInverterModeOverridden(false); // <---
        dateTime.DateTimeTestValue = DateTime.Parse("2024-01-01 16:00"); // <---
        var tokenSource = new CancellationTokenSource();
        tokenSource.CancelAfter(TimeSpan.FromMilliseconds(300));
        await application.ExecuteAsyncTest(tokenSource.Token);

        // Check the logger to see that inverter was set to Inverter Only
        Assert.IsTrue(mqttClient.PublishAsyncCalls > 0);
        Assert.IsTrue(application.GetInverterModeOverridden());
    }

    [TestMethod]
    public async Task Application_TODInRange_InverterAlreadyOverridden_Test()
    {
        // Inverter should not be changed
        var asyncDelay = new TestAsyncDelay();
        var loggerFactory = new TestLoggerFactory();
        var mqttClient = new MqttTestClient();
        var flashMqClient = new FlashMqClientTester(mqttClient, loggerFactory, asyncDelay);
        var dateTime = new TestDateTime();
        var application = new ApplicationTester(configuration!, loggerFactory, flashMqClient, asyncDelay, dateTime);

        flashMqClient.SetGeneratorState(GeneratorState.Stopped);
        flashMqClient.SetSoc(90);
        flashMqClient.SetInverterMode(InverterMode.InverterOnly); // <---
        application.SetInverterModeOverridden(true); // <---
        dateTime.DateTimeTestValue = DateTime.Parse("2024-01-01 16:00"); // <---
        var tokenSource = new CancellationTokenSource();
        tokenSource.CancelAfter(TimeSpan.FromMilliseconds(300));
        await application.ExecuteAsyncTest(tokenSource.Token);

        // Check the logger to see that no commands were sent. (1 is for initial connection keep alive)
        Assert.IsTrue(mqttClient.PublishAsyncCalls == 1);
        Assert.IsTrue(application.GetInverterModeOverridden());
        Assert.IsTrue(loggerFactory.Logger.LogMessages.Last().Contains("Inverter mode is not On and will not be changed"));
    }

    [TestMethod]
    public async Task Application_TODOutOfRange_InverterReset_Test()
    {
        // Inverter should be reset to On
        var asyncDelay = new TestAsyncDelay();
        var loggerFactory = new TestLoggerFactory();
        var mqttClient = new MqttTestClient();
        var flashMqClient = new FlashMqClientTester(mqttClient, loggerFactory, asyncDelay);
        var dateTime = new TestDateTime();
        var application = new ApplicationTester(configuration!, loggerFactory, flashMqClient, asyncDelay, dateTime);

        flashMqClient.SetGeneratorState(GeneratorState.Stopped);
        flashMqClient.SetSoc(90);
        flashMqClient.SetInverterMode(InverterMode.On); // <---
        application.SetInverterModeOverridden(false); // <---
        
        // Turn inverter to inverter only
        dateTime.DateTimeTestValue = DateTime.Parse("2024-01-01 16:00"); // <---
        var tokenSource = new CancellationTokenSource();
        tokenSource.CancelAfter(TimeSpan.FromMilliseconds(300));
        await application.ExecuteAsyncTest(tokenSource.Token);
        flashMqClient.SetInverterMode(InverterMode.InverterOnly); // <---

        Assert.IsTrue(application.GetInverterModeOverridden());

        dateTime.DateTimeTestValue = DateTime.Parse("2024-01-01 20:01"); // <---
        tokenSource = new CancellationTokenSource();
        tokenSource.CancelAfter(TimeSpan.FromMilliseconds(300));
        // Turn inverter to ON
        await application.ExecuteAsyncTest(tokenSource.Token);

        // Check the command to set to inverter only then to ON. (1 is for initial connection keep alive)
        Assert.IsTrue(mqttClient.PublishAsyncCalls > 3);
        Assert.IsFalse(application.GetInverterModeOverridden());
    }

    [TestMethod]
    public async Task Application_TODOutOfRange_InverterNotOverridden_Test()
    {
        // Inverter should not be changed
        var asyncDelay = new TestAsyncDelay();
        var loggerFactory = new TestLoggerFactory();
        var mqttClient = new MqttTestClient();
        var flashMqClient = new FlashMqClientTester(mqttClient, loggerFactory, asyncDelay);
        var dateTime = new TestDateTime();
        var application = new ApplicationTester(configuration!, loggerFactory, flashMqClient, asyncDelay, dateTime);

        flashMqClient.SetGeneratorState(GeneratorState.Stopped);
        flashMqClient.SetSoc(90);

        // Turn inverter to inverter only
        dateTime.DateTimeTestValue = DateTime.Parse("2024-01-01 20:01"); // <---
        var tokenSource = new CancellationTokenSource();
        tokenSource.CancelAfter(TimeSpan.FromMilliseconds(300));
        flashMqClient.SetInverterMode(InverterMode.On); // <---
        application.SetInverterModeOverridden(false); // <---

        await application.ExecuteAsyncTest(tokenSource.Token);

        //  (1 is for initial connection keep alive)
        Assert.IsTrue(mqttClient.PublishAsyncCalls == 1);
    }
}
