using Castle.DynamicProxy;
using MhpdCommon.Models.Configuration;
using MhpdCommon.Models.MessageBodyModels;
using MhpdCommon.Models.MHPDModels;
using MhpdCommon.Repository;
using MhpdCommon.SharedHttpClient;
using MhpdCommon.TokenValidation;
using MhpdCommon.Utils;
using Microsoft.AspNetCore.Builder.Extensions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using PensionsRetrievalFunction.Orchestration;
using PensionsRetrievalFunction.Repository;
using System.Linq;
using System.Net;
using ResponseMessage = MhpdCommon.Models.MHPDModels.ResponseMessage;

namespace PensionsRetrievalFunctionTests;

public class PeiIntegrationOrchestratorTests
{
    private readonly Mock<ILogger<PeiIntegrationOrchestrator>> _logger = new();
    private readonly Mock<IMessagingService> _messagingService = new();
    private readonly Mock<IPensionRetrievalRepository> _repository = new();
    private readonly Mock<ICosmosDbRepository<UserSessionData>> _mockUserSessionDataRepository = new();
    private const string InboundQueue = "data-in";
    private const string OutboundQueue = "data-out";

    public PeiIntegrationOrchestratorTests()
    {
        _messagingService.Setup(mock => mock.SendMessageAsync(It.IsAny<PensionRequestPayload>(), OutboundQueue, It.IsAny<string>(), It.IsAny<DateTimeOffset>())).Verifiable();

        var testInstanceData = new UserSessionData
        {
            UserSessionId = Guid.NewGuid().ToString(),
            AccessToken = TokenQueryParams.ValidJwtToken
        };

        _mockUserSessionDataRepository.Setup(x => x.GetByIdAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(testInstanceData);
    }


    [Fact]
    public async Task WhenSchedulingMessages_ShouldQueueTheCorrectNumber()
    {
        // Arrange
        var apiConfiguration = new PeiOrchestrationSettings
        {
            PeiPollingInterval = 5,
            PeiRetrievalDuration = 60
        };

        var sbConfiguration = new CommonServiceBusConfiguration
        {
            InboundQueue = InboundQueue,
            OutboundQueue = OutboundQueue
        };


        var client = CreateHttpClientWithRetry(1);

        var apiOptions = Options.Create(apiConfiguration);
        var sbOptions = Options.Create(sbConfiguration);


        var orchestrator = new PeiIntegrationOrchestrator(sbOptions, apiOptions, _messagingService.Object,
            client.Object, _repository.Object, _logger.Object, _mockUserSessionDataRepository.Object);

        var correlationId = Guid.NewGuid().ToString();

        var payload = new PensionRetrievalPayload
        {
            Iss = "Test ISS",
            PeisId = Guid.NewGuid().ToString(),
            UserSessionId = correlationId
        };

        var record = new PensionsRetrievalRecord
        {
            Id = Guid.NewGuid().ToString(),
            Iss = payload.Iss,
            PeisId = payload.PeisId,
            UserSessionId = payload.UserSessionId
        };

        _repository.Setup(mock => mock.CreateRecordIfNotExistsAsync(It.IsAny<PensionRetrievalPayload>())).ReturnsAsync(record);

        // Act
        var resultRecord = await orchestrator.ScheduleMessagesAsync(payload, correlationId);

        // Assert
        _messagingService.Verify(mock => mock.SendMessagesAsync(It.Is<OutboundServiceBusMessage<PensionRetrievalMessagePayload>[]>(messages => messages.Count() == 12), InboundQueue), Times.Once);
        Assert.Equal(record, resultRecord);
    }

    [Fact]
    public async Task WhenHttpClientIsExecutedWithRetryConfiguration_EndpointIsCalledAsExpected()
    {
        // Arrange
        var apiConfiguration = new PeiOrchestrationSettings
        {
            PeiPollingInterval = 5,
            PeiRetrievalDuration = 60
        };

        var sbConfiguration = new CommonServiceBusConfiguration
        {
            InboundQueue = InboundQueue,
            OutboundQueue = OutboundQueue
        };

        var client = new Mock<IPeiServiceClient>();
        client.Setup(mock => mock.GetPeiDataAsync(It.IsAny<PeiRequestModel>())).ReturnsAsync(CreateResponse(true, false));

        var apiOptions = Options.Create(apiConfiguration);
        var sbOptions = Options.Create(sbConfiguration);

        var payload = new PensionRetrievalMessagePayload
        {
            Iss = "Test ISS",
            PeisId = Guid.NewGuid().ToString(),
            UserSessionId = Guid.NewGuid().ToString(),
            AccessToken = "access_token",
            AttemptNumber = 1
        };

        var record = new PensionsRetrievalRecord
        {
            Id = Guid.NewGuid().ToString(),
            Iss = payload.Iss,
            PeisId = payload.PeisId,
            UserSessionId = payload.UserSessionId
        };

        _repository.Setup(mock => mock.GetRetrievalRecordAsync(payload.UserSessionId)).ReturnsAsync(record);

        var orchestrator = new PeiIntegrationOrchestrator(sbOptions, apiOptions, _messagingService.Object,
            client.Object, _repository.Object, _logger.Object, _mockUserSessionDataRepository.Object);

        var correlationId = Guid.NewGuid().ToString();

        // Act
        await orchestrator.RunAsync(payload, correlationId);

        // Assert
        client.Verify(mock => mock.GetPeiDataAsync(It.Is<PeiRequestModel>(request => request.Iss == payload.Iss && request.PeisId == payload.PeisId && request.UserSessionId == payload.UserSessionId)), Times.Once);
        _messagingService.Verify(mock => mock.SendMessagesAsync(It.Is<OutboundServiceBusMessage<PensionRequestPayload>[]>(messages => messages.Count() == 1 && messages.Single().CorrelationId == correlationId), OutboundQueue), Times.Once);
        _repository.Verify(mock => mock.UpdatePensionsRetrievalRecordAsync(It.IsAny<PensionsRetrievalRecord>()), Times.Once);
    }

    [Fact]
    public async Task ScheduleAndProcessingShouldResultInPeiRetrievalCompletedStatus()
    {
        // Arrange
        var apiConfiguration = new PeiOrchestrationSettings
        {
            PeiPollingInterval = 5,
            PeiRetrievalDuration = 60
        };
        var sbConfiguration = new CommonServiceBusConfiguration
        {
            InboundQueue = InboundQueue,
            OutboundQueue = OutboundQueue
        };

        var client = new Mock<IPeiServiceClient>(); 
        client.Setup(mock => mock.GetPeiDataAsync(It.IsAny<PeiRequestModel>())).ReturnsAsync(CreateResponse(true, false));

        var apiOptions = Options.Create(apiConfiguration);
        var sbOptions = Options.Create(sbConfiguration);
        var orchestrator = new PeiIntegrationOrchestrator(sbOptions, apiOptions, _messagingService.Object, client.Object, _repository.Object, _logger.Object, _mockUserSessionDataRepository.Object);
        var correlationId = Guid.NewGuid().ToString();
        var payload = new PensionRetrievalPayload
        {
            Iss = "Test ISS",
            PeisId = Guid.NewGuid().ToString(),
            UserSessionId = correlationId
        };

        var record = new PensionsRetrievalRecord
        {
            Id = Guid.NewGuid().ToString(),
            Iss = payload.Iss,
            PeisId = payload.PeisId,
            UserSessionId = payload.UserSessionId
        };
        _repository.Setup(mock => mock.CreateRecordIfNotExistsAsync(It.IsAny<PensionRetrievalPayload>())).ReturnsAsync(record);

        // Act 1
        var resultRecord = await orchestrator.ScheduleMessagesAsync(payload, correlationId);

        // Arrange
        var sentMessages = _messagingService.Invocations.Where(invocation => invocation.Method.Name == nameof(_messagingService.Object.SendMessagesAsync)).SelectMany(invocation => invocation.Arguments.OfType<OutboundServiceBusMessage<PensionRetrievalMessagePayload>[]>().Single()).ToList();

        var updatedRecordHistory = new List<PensionsRetrievalRecord>();
        _repository.Setup(mock => mock.UpdatePensionsRetrievalRecordAsync(It.IsAny<PensionsRetrievalRecord>())).Callback<PensionsRetrievalRecord>(r =>
        {
            updatedRecordHistory.Add(new PensionsRetrievalRecord
            {
                PeiRetrievalComplete = r.PeiRetrievalComplete
            });
        }).Returns(Task.CompletedTask);
        _repository.Setup(mock => mock.GetRetrievalRecordAsync(payload.UserSessionId)).ReturnsAsync(record);

        Assert.True(sentMessages.Count == 12);

        // Act 2
        foreach (var message in sentMessages.OrderBy(m => m.ScheduledEnqueueTime))
        {
            await orchestrator.RunAsync(message.Payload, correlationId);
        }

        // Assert
        // Only 1 unique record, so only 1 update, followed by the complete update on the last message
        Assert.True(updatedRecordHistory.Count(r => !r.PeiRetrievalComplete) == 1);
        Assert.True(updatedRecordHistory.Count(r => r.PeiRetrievalComplete) == 1);
    }


    private static Mock<IPeiServiceClient> CreateHttpClientWithRetry(int simulationAttempts)
    {
        var httpClientMock = new Mock<IPeiServiceClient>();

        var attempts = 1;

        var sequence = httpClientMock
            .SetupSequence(mock => mock.GetPeiDataAsync(It.IsAny<PeiRequestModel>()));

        while (simulationAttempts > attempts)
        {
            var withData = attempts % 2 == 0;
            var isErrorOverride = attempts == 3; //make all 3rd attempts fail with error

            sequence = sequence.ReturnsAsync(CreateResponse(withData, isErrorOverride));
            attempts++;
        }

        return httpClientMock;
    }

    private static CdaPeisServiceResponseModel CreateResponse(bool withData, bool isErrorOverride)
    {
        if(isErrorOverride)
        {
            return new CdaPeisServiceResponseModel
            {
                Peis = null,
                ResponseMessage = new ResponseMessage
                {
                    ResponseStatusCode = HttpStatusCode.InternalServerError
                }
            };
        }

        var response = new List<PeiDataModel>
        {
            new() {
                Description = "Test",
                Pei = Guid.NewGuid().ToString(),
                RetrievalRequestedTimestamp = DateTime.UtcNow,
                RetrievalStatus = "Started"
            }
        };
        return new CdaPeisServiceResponseModel
        {
            Peis = withData ? [.. response] : [],
            ResponseMessage = new ResponseMessage
            {
                ResponseStatusCode = HttpStatusCode.OK,
            }
        };
    }
}
