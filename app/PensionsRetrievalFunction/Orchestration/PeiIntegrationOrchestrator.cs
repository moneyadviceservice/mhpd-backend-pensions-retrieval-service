using MhpdCommon.Models.Configuration;
using MhpdCommon.Models.MessageBodyModels;
using MhpdCommon.Models.MHPDModels;
using MhpdCommon.Repository;
using MhpdCommon.SharedHttpClient;
using MhpdCommon.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PensionsRetrievalFunction.Models;
using PensionsRetrievalFunction.Repository;
using Polly;

namespace PensionsRetrievalFunction.Orchestration;

public class PeiIntegrationOrchestrator(IOptions<CommonServiceBusConfiguration> sbOptions, 
    IOptions<PeiOrchestrationSettings> peiOptions,
    IMessagingService messagingService, 
    IPeiServiceClient client, 
    IPensionRetrievalRepository repository,
    ILogger<PeiIntegrationOrchestrator> logger,
    ICosmosDbRepository<UserSessionData> userSessionDataRepository) : IPeiIntegrationOrchestrator
{
    private readonly CommonServiceBusConfiguration _serviceBusConfiguration = sbOptions.Value;
    private readonly PeiOrchestrationSettings _settings = peiOptions.Value;

    public async Task<PensionsRetrievalRecord?> ScheduleMessagesAsync(PensionRetrievalPayload payload, string correlationId)
    {
        var record = await repository.CreateRecordIfNotExistsAsync(payload);
        if (record == null)
        {
            logger.LogWarning("Pension retrieval record already exists for session: {Session}. Skipping further processing...", payload.UserSessionId);
            return null;
        }

        // Fetch userSessionData and get the access_token that was stored during the pensions-data-retrieval
        var userSessionId = payload.UserSessionId!;

        var userSessionData = await userSessionDataRepository.GetByIdAsync(userSessionId, userSessionId);
        if (userSessionData == null)
        {
            logger.LogError("Error retrieving UserSessionData for Id {UserSessionId}", userSessionId);
            return null;
        }

        if (string.IsNullOrEmpty(userSessionData.AccessToken))
        {
            logger.LogError("Error retrieving Access Token from UserSessionData for Id {UserSessionId}", userSessionId);
            return null;
        }

        var messagesToSend = Enumerable.Range(0, peiOptions.Value.RetryLimit + 1).Select(i => 
                new OutboundServiceBusMessage<PensionRetrievalMessagePayload>
                {
                    Payload = new PensionRetrievalMessagePayload
                    {
                        PeisId = payload.PeisId,
                        Iss = payload.Iss,
                        UserSessionId = userSessionId,
                        AccessToken = userSessionData.AccessToken,
                        AttemptNumber = i + 1
                    },
                    ScheduledEnqueueTime = DateTimeOffset.UtcNow.AddSeconds(i * peiOptions.Value.PeiPollingInterval),
                    CorrelationId = correlationId
                }
            ).ToArray();

        await messagingService.SendMessagesAsync(messagesToSend, _serviceBusConfiguration.InboundQueue!);

        return record;
    }

    public async Task RunAsync(PensionRetrievalMessagePayload payload, string correlationId)
    {
        logger.LogWarning("Attempt #{AttemptCount} to fetch PEI data for user session {SessionId}...", payload.AttemptNumber, payload.UserSessionId);

        var peiResponse = new PeiDataResponse(payload.AccessToken, []);

        var record = await repository.GetRetrievalRecordAsync(payload.UserSessionId!);
        if (record == null)
        {
            logger.LogWarning("Pension retrieval record does not exist for session: {Session}. Skipping further processing...", payload.UserSessionId);
            return;
        }

        var response = await client.GetPeiDataAsync(new PeiRequestModel
        {
            Iss = payload.Iss!,
            Rpt = peiResponse.Rpt!, // RPT == Access_token
            CorrelationId = correlationId,
            UserSessionId = payload.UserSessionId!,
            PeisId = payload.PeisId!,
        });

        var fetchedPeis = record.PeiData.Select(p => p.Pei).ToHashSet();
        var recordUpdated = false;
        var messagesToSend = new List<OutboundServiceBusMessage<PensionRequestPayload>>();
        foreach (var pei in response.Peis!.Where(peis => !fetchedPeis.Contains(peis.Pei)))
        {
            pei.RetrievalStatus = Constants.RetrievalStatus.Requested;
            pei.RetrievalRequestedTimestamp = DateTime.UtcNow;

            if (peiResponse.TryAdd(pei))
            {
                var message = CreateRequestPayload(pei, record);
                logger.LogWarning("Pension details request sent for PEI {Pei} with retrieval Id {Id}", message.Pei, message.PensionRetrievalRecordId);
                messagesToSend.Add(new OutboundServiceBusMessage<PensionRequestPayload>
                {
                    Payload = message,
                    CorrelationId = correlationId
                });
                record!.PeiData.Add(pei);
                recordUpdated = true;
            }
        }

        if (messagesToSend.Count > 0)
        {
            await messagingService.SendMessagesAsync(messagesToSend.ToArray(), _serviceBusConfiguration.OutboundQueue!);
        }

        if (payload.AttemptNumber == _settings.RetryLimit + 1)
        {
            record!.PeiRetrievalComplete = true;
            recordUpdated = true;
        }

        if (recordUpdated)
        {
            await repository.UpdatePensionsRetrievalRecordAsync(record);
        }
    }

    private static PensionRequestPayload CreateRequestPayload(PeiDataModel pei, PensionsRetrievalRecord record)
    {
        return new PensionRequestPayload
        {
            Iss = record.Iss,
            Pei = pei.Pei,
            PensionRetrievalRecordId = record.Id,
            UserSessionId = record.UserSessionId
        };
    }
}
