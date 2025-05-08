using Azure.Messaging.ServiceBus;
using MhpdCommon.Models.MessageBodyModels;
using MhpdCommon.Utils;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Moq;
using PensionsRetrievalFunction;
using PensionsRetrievalFunction.Orchestration;
using PensionsRetrievalFunctionTests.Data;

namespace PensionsRetrievalFunctionTests;

public class RetrievalFunctionTests
{
    private readonly Mock<ServiceBusMessageActions> _actionsMock;
    private readonly Mock<IIdValidator> _idValidatorMock;
    private readonly Mock<IMessageParser> _messageParseMock;
    private readonly Mock<ILogger<RetrievalFunction>> _loggerMock;
    private readonly Mock<IPeiIntegrationOrchestrator> _orchestratorMock;
    private readonly Mock<IPeiIntegrationOrchestrator> _integrationServiceClientMock;
    private readonly RetrievalFunction _function;

    public RetrievalFunctionTests()
    {
        _actionsMock = new Mock<ServiceBusMessageActions>();
        _actionsMock.Setup(x => x.DeadLetterMessageAsync(It.IsAny<ServiceBusReceivedMessage>(),
            null, It.IsAny<string>(), null, It.IsAny<CancellationToken>())).Verifiable();
        _actionsMock.Setup(x => x.AbandonMessageAsync(It.IsAny<ServiceBusReceivedMessage>(),
            null, It.IsAny<CancellationToken>())).Verifiable();
        _actionsMock.Setup(x => x.CompleteMessageAsync(It.IsAny<ServiceBusReceivedMessage>(), It.IsAny<CancellationToken>())).Verifiable();

        _loggerMock = new Mock<ILogger<RetrievalFunction>>();
        _idValidatorMock = new Mock<IIdValidator>();
        _messageParseMock = new Mock<IMessageParser>();
        _orchestratorMock = new Mock<IPeiIntegrationOrchestrator>();

        _idValidatorMock.Setup(x => x.IsValidGuid(It.IsAny<string>())).Returns(false);
        _idValidatorMock.Setup(x => x.IsValidPeI(It.IsAny<string>())).Returns(false);

        var error = new AggregateException(new Exception("Bad Data"));
        _messageParseMock.Setup(x => x.ToPensionRetrievalPayload(It.IsAny<string>())).Throws(error);

        _orchestratorMock.Setup(mock => mock.RunAsync(It.IsAny<PensionRetrievalPayload>(), It.IsAny<string>())).Verifiable();

        _integrationServiceClientMock = new Mock<IPeiIntegrationOrchestrator>();

        _function = new RetrievalFunction(_loggerMock.Object, _idValidatorMock.Object, 
            _messageParseMock.Object, _orchestratorMock.Object);
    }

    [Fact]
    public async Task Run_ShouldCallDeadLetterQueue_OnNoCorrelationId()
    {
        ResetInvocations();

        //arrange
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: new BinaryData("Test message"));

        // Act
        await _function.Run(message, _actionsMock.Object);

        // Assert
        var reason = "Missing or Invalid correlationId:";
        _actionsMock.Verify(r => r.DeadLetterMessageAsync(message, null,
            It.Is<string>(arg => arg.StartsWith(reason)), null, It.IsAny<CancellationToken>()), Times.Once);
        _actionsMock.Verify(r => r.CompleteMessageAsync(message, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("BadPeisIdPensionRetrievalMessage.json")]
    [InlineData("EmptyIssPensionRetrievalMessage.json")]
    [InlineData("NullSessionPensionRetrievalMessage.json")]
    public async Task Run_ShouldCallDeadLetterQueue_OnPayloadParseFail(string file)
    {
        ResetInvocations();

        //arrange
        _idValidatorMock.Setup(x => x.IsValidGuid(It.IsAny<string>())).Returns(true);

        var content = DataProvider.GetString(file);
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString(content), correlationId: Guid.NewGuid().ToString());

        // Act
        await _function.Run(message, _actionsMock.Object);

        // Assert
        _actionsMock.Verify(r => r.DeadLetterMessageAsync(message, null,
            It.Is<string>(arg => arg.StartsWith("Invalid pension retrieval payload")), null, It.IsAny<CancellationToken>()), Times.Once);
        _actionsMock.Verify(r => r.CompleteMessageAsync(message, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Run_ShouldCallCompleteMessage_OnSaveSuccess()
    {
        ResetInvocations();

        //arrange
        var reason = string.Empty;
        const string file = "ValidPensionRetrievalMessage.json";
        var payload = DataProvider.GetPayload(file);

        _idValidatorMock.Setup(x => x.IsValidGuid(It.IsAny<string>())).Returns(true);
        _messageParseMock.Setup(x => x.ToPensionRetrievalPayload(It.IsAny<string>())).Returns(payload);

        var content = DataProvider.GetString(file);
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString(content), correlationId: Guid.NewGuid().ToString());

        // Act
        await _function.Run(message, _actionsMock.Object);

        // Assert
        _actionsMock.Verify(r => r.CompleteMessageAsync(message, It.IsAny<CancellationToken>()), Times.Once);
    }

    private void ResetInvocations()
    {
        _actionsMock.Invocations.Clear();
        _loggerMock.Invocations.Clear();
    }
}