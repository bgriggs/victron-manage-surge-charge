using BigMission.TestHelpers.Delay;
using Microsoft.Extensions.Logging;
using Moq;

namespace VictronManageSurgeRates.Tests;

[TestClass]
public sealed class FlashMqClientTests
{
    private Mock<ILoggerFactory>? loggerFactoryMock;
    private Mock<ILogger>? loggerMock;

    [TestInitialize]
    public void Setup()
    {
        loggerFactoryMock = new Mock<ILoggerFactory>();
        loggerMock = new Mock<ILogger>();
        loggerFactoryMock.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(loggerMock.Object);
    }

    [TestMethod]
    public async Task ConnectTest()
    {
        var mqttClient = new MqttTestClient();
        var flashMqClient = new FlashMqClientTester(mqttClient, loggerFactoryMock!.Object, new TestAsyncDelay());
        await flashMqClient.Connect("1.1.1.1", "12345", CancellationToken.None);

        Assert.AreEqual(1, mqttClient.ConnectAsyncCalls);
        Assert.AreEqual(5, mqttClient.SubscribeAsyncCalls);
        Assert.AreEqual(1, mqttClient.PublishAsyncCalls);
    }

    /// <summary>
    /// Test for when the keep alive publish has not returned with a response.
    /// </summary>
    /// <returns></returns>
    [TestMethod]
    public async Task ExecuteAsync_PublishPendingTest()
    {
        var mqttClient = new MqttTestClient();
        var flashMqClient = new FlashMqClientTester(mqttClient, loggerFactoryMock!.Object, new TestAsyncDelay());
        mqttClient.IsConnected = true;
        flashMqClient.SetPublishCompleted(false);
        var tokenSource = new CancellationTokenSource();
        tokenSource.CancelAfter(TimeSpan.FromMilliseconds(10));
        await flashMqClient.ExecuteAsyncTest(tokenSource.Token);

        // Make sure it skips publishing the suppress keep alive
        Assert.AreEqual(0, mqttClient.PublishAsyncCalls);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReconnectTest()
    {
        var mqttClient = new MqttTestClient();
        var flashMqClient = new FlashMqClientTester(mqttClient, loggerFactoryMock!.Object, new TestAsyncDelay());

        await flashMqClient.Connect("1.1.1.1", "12345", CancellationToken.None);
        mqttClient.IsConnected = false; // Set to disconnected
        flashMqClient.SetPublishCompleted(true);

        var tokenSource = new CancellationTokenSource();
        tokenSource.CancelAfter(TimeSpan.FromMilliseconds(10));
        await flashMqClient.ExecuteAsyncTest(tokenSource.Token);

        // Make sure it skips publishing the suppress keep alive
        Assert.IsTrue(mqttClient.ConnectAsyncCalls >= 1);
        Assert.IsTrue(mqttClient.SubscribeAsyncCalls >= 4);
        Assert.IsTrue(mqttClient.PublishAsyncCalls >= 1);
    }

    [TestMethod]
    public async Task ExecuteAsync_NotInitializedDisconnectedTest()
    {
        var mqttClient = new MqttTestClient();
        var flashMqClient = new FlashMqClientTester(mqttClient, loggerFactoryMock!.Object, new TestAsyncDelay());

        mqttClient.IsConnected = false; // Set to disconnected
        flashMqClient.SetPublishCompleted(true);

        var tokenSource = new CancellationTokenSource();
        tokenSource.CancelAfter(TimeSpan.FromMilliseconds(10));
        await flashMqClient.ExecuteAsyncTest(tokenSource.Token);

        // Make sure it does not try to connect until initialized
        Assert.IsTrue(mqttClient.ConnectAsyncCalls == 0);
        Assert.IsTrue(mqttClient.SubscribeAsyncCalls == 0);
        Assert.IsTrue(mqttClient.PublishAsyncCalls == 0);
    }
}
