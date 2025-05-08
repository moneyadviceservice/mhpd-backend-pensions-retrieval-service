using System.Net;
using MhpdCommon.Models.Configuration;
using MhpdCommon.Models.MessageBodyModels;
using MhpdCommon.Models.MHPDModels;
using MhpdCommon.Repository;
using MhpdCommon.SharedHttpClient;
using MhpdCommon.TokenValidation;
using MhpdCommon.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using PensionsRetrievalFunction.Orchestration;
using PensionsRetrievalFunction.Repository;
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
        _logger = new ();
        _repository = new();
        _mockUserSessionDataRepository = new();
        _messagingService = new Mock<IMessagingService>();
        _messagingService.Setup(mock => mock.SendMessageAsync(It.IsAny<PensionRequestPayload>(), OutboundQueue, It.IsAny<string>())).Verifiable();

        var testInstanceData = new UserSessionData
        {
            UserSessionId = Guid.NewGuid().ToString(),
            AccessToken = TokenQueryParams.ValidJwtToken
        };

        _mockUserSessionDataRepository.Setup(x => x.GetByIdAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(testInstanceData);
    }

    [Theory]
    [InlineData(4, 5, 3, 1, 2)]
    [InlineData(7, 10, 6, 3, 4)]
    [InlineData(9, 15, 8, 4, 5)]
    [InlineData(12, 20, 11, 5, 6)]
    public async Task WhenHttpClientIsExecutedWithRetryConfiguration_EndpointIsCalledAsExpected(
        int callsToSimulate, int timeout, int expectedClientCallCount, int expectedMessagingCallCount, int expectedSaveCount)
    {
        //Arrange
        var apiConfiguration = new PeiOrchestrationSettings
        {
            PeiPollingInterval = 2,
            PeiRetrievalDuration = timeout
        };

        var sbConfiguration = new CommonServiceBusConfiguration
        {
            InboundQueue = InboundQueue,
            OutboundQueue = OutboundQueue
        };

        var client = CreateHttpClientWithRetry(callsToSimulate);

        var apiOptions = Options.Create(apiConfiguration);
        var sbOptions = Options.Create(sbConfiguration);

        var payload = new PensionRetrievalPayload
        {
            Iss = "Test ISS",
            PeisId = Guid.NewGuid().ToString(),
            UserSessionId = Guid.NewGuid().ToString()
        };

        var record = new PensionsRetrievalRecord
        {
            Id = Guid.NewGuid().ToString(),
            Iss = payload.Iss,
            PeisId = payload.PeisId,
            UserSessionId = payload.UserSessionId
        };

        _repository.Setup(mock => mock.CreateRecordIfNotExistsAsync(It.IsAny<PensionRetrievalPayload>())).ReturnsAsync(record);
        _repository.Setup(mock => mock.UpdatePensionsRetrievalRecordAsync(It.IsAny<PensionsRetrievalRecord>())).Verifiable();

        var orchestrator = new PeiIntegrationOrchestrator(sbOptions, apiOptions, _messagingService.Object,
            client.Object, _repository.Object, _logger.Object, _mockUserSessionDataRepository.Object);

        var correlationId = Guid.NewGuid().ToString();

        //Act
        await orchestrator.RunAsync(payload, correlationId);

        //Assert
        client.Verify(mock => mock.GetPeiDataAsync(It.Is<PeiRequestModel>(request => request.Iss == payload.Iss && 
        request.PeisId == payload.PeisId && request.UserSessionId == payload.UserSessionId)), Times.Exactly(expectedClientCallCount));

        _messagingService.Verify(mock => mock.SendMessageAsync(It.IsAny<PensionRequestPayload>(), OutboundQueue, correlationId),
            Times.Exactly(expectedMessagingCallCount));

        _repository.Verify(mock => mock.UpdatePensionsRetrievalRecordAsync(It.IsAny<PensionsRetrievalRecord>()), Times.Exactly(expectedSaveCount));
    }


    private static Mock<IPeiServiceClient> CreateHttpClientWithRetry(int simulationAttempts)
    {
        var httpClientMock = new Mock<IPeiServiceClient>();

        var attempts = 1;

        var sequence = httpClientMock
            .SetupSequence(mock => mock.GetPeiDataAsync(It.IsAny<PeiRequestModel>()));

        while (simulationAttempts > attempts)
        {
            sequence = sequence.ReturnsAsync(CreateResponse(attempts % 2 == 0));
            attempts++;
        }

        return httpClientMock;
    }

    private static CdaPeisServiceResponseModel CreateResponse(bool withData)
    {
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
            Peis = withData ? response.ToArray() : [],
            ResponseMessage = new ResponseMessage
            {
                ResponseStatusCode = HttpStatusCode.OK,
            }
        };
    }
}
