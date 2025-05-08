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

    public async Task RunAsync(PensionRetrievalPayload payload, string correlationId)
    {
        var record = await repository.CreateRecordIfNotExistsAsync(payload);
        if (record == null)
        {
            logger.LogWarning("Pension retrieval record already exists for session: {Session}. Skipping further processing...", payload.UserSessionId);
            return;
        }
        
        // Fetch userSessionData and get the access_token that was stored during the pensions-data-retrieval
        var userSessionId = payload.UserSessionId!;
        
        var userSessionData = await userSessionDataRepository.GetByIdAsync(userSessionId,  userSessionId);
        if (userSessionData == null)
        {
            logger.LogError("Error retrieving UserSessionData for Id {UserSessionId}", userSessionId);
            return;
        }

        if (string.IsNullOrEmpty(userSessionData.AccessToken))
        {
            logger.LogError("Error retrieving Access Token from UserSessionData for Id {UserSessionId}", userSessionId);
            return;
        }
        
        var peiResponse = new PeiDataResponse(userSessionData.AccessToken, []);

        var retryCondition = new Func<PeiDataResponse, bool>(_ => true);

        var retryPolicy = Policy
            .HandleResult(retryCondition)
            .WaitAndRetryAsync(
                retryCount: _settings.RetryLimit,
                sleepDurationProvider: _ => TimeSpan.FromSeconds(_settings.PeiPollingInterval),
                onRetry: (_, _, attemptCount, _) =>
                {
                    logger.LogWarning("Retry attempt #{AttemptCount} to fetch PEI data for user session {SessionId}", attemptCount, payload.UserSessionId);
                }
            );

        try
        {
            await retryPolicy.ExecuteAsync(async () =>
            {
                var response = await client.GetPeiDataAsync(new PeiRequestModel
                {
                    Iss = payload.Iss!,
                    Rpt = peiResponse.Rpt!, // RPT == Access_token
                    CorrelationId = correlationId,
                    UserSessionId = payload.UserSessionId!,
                    PeisId = payload.PeisId!,
                });
                
                foreach (var pei in response.Peis!)
                {
                    pei.RetrievalStatus = Constants.RetrievalStatus.Requested;
                    pei.RetrievalRequestedTimestamp = DateTime.UtcNow;

                    if (peiResponse.TryAdd(pei))
                    {
                        var message = CreateRequestPayload(pei, record);
                        logger.LogWarning("Pension details request sent for PEI {Pei} with retrieval Id {Id}"
                            , message.Pei, message.PensionRetrievalRecordId);
                        await messagingService.SendMessageAsync(message, _serviceBusConfiguration.OutboundQueue!, correlationId);

                        record.PeiData.Add(pei);
                        await repository.UpdatePensionsRetrievalRecordAsync(record);
                    }
                }
                return peiResponse;
            });

            record.PeiRetrievalComplete = true;
            await repository.UpdatePensionsRetrievalRecordAsync(record);
        }
        catch (Exception error)
        {
            logger.LogError(error, "Error retrieving PEI data for Id {PeisId}", payload.PeisId);
        }

        logger.LogWarning("Pei request orchestration complete");
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
