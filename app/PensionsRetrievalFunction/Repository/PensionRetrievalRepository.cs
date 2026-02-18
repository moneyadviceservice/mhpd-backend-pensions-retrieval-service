using MhpdCommon.Models.Configuration;
using MhpdCommon.Models.MessageBodyModels;
using MhpdCommon.Models.MHPDModels;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PensionsRetrievalFunction.Repository;

public class PensionRetrievalRepository(ILogger<PensionRetrievalRepository> logger, IOptions<CosmosBusinessConfiguration> cosmosBusinessConfiguration, CosmosClient cosmosClient) 
    : IPensionRetrievalRepository
{
    private readonly Container _container = cosmosClient.GetContainer(cosmosBusinessConfiguration.Value.DatabaseId, cosmosBusinessConfiguration.Value.PensionsRetrievalContainer);

    public async Task<PensionsRetrievalRecord?> CreateRecordIfNotExistsAsync(PensionRetrievalPayload payload)
    {
        var response = await GetMatchingRecordsAsync(payload.UserSessionId!);
        if(response.Count == 0)
        {
            var record = CreateRecord(payload);

            var writeResponse = await _container.CreateItemAsync(
                item: record,
                partitionKey: new PartitionKey(record.UserSessionId)
            );

            logger.LogWarning("Created new PensionsRetrievalRecord with id {RecordId} for user session {UserSessionId}", record.Id, record.UserSessionId);
            return writeResponse.Resource;
        }

        return null;
    }

    public Task UpdatePensionsRetrievalRecordAsync(PensionsRetrievalRecord record)
    {
        return _container.ReplaceItemAsync(record, record.Id, new PartitionKey(record.UserSessionId), null, default);
    }

    public async Task<PensionsRetrievalRecord?> GetRetrievalRecordAsync(string userSessionId)
    {
        var response = await GetMatchingRecordsAsync(userSessionId);
        var result = response.SingleOrDefault();

        if(result == null)
        {
            logger.LogWarning("No PensionsRetrievalRecord found for user session {UserSessionId}", userSessionId);
        }

        return result;
    }

    public async Task DeleteRetrievalRecordsAsync(string userSessionId)
    {
        var response = await _container.DeleteAllItemsByPartitionKeyStreamAsync(new PartitionKey(userSessionId));
        if (!response.IsSuccessStatusCode)
        {
            throw new CosmosException(
                response.ErrorMessage,
                response.StatusCode,
                0,
                response.Headers.ActivityId,
                response.Headers.RequestCharge);
        }
    }

    private async Task<FeedResponse<PensionsRetrievalRecord>> GetMatchingRecordsAsync(string userSessionId)
    {
        var query = new QueryDefinition("SELECT TOP 1 * FROM c WHERE c.userSessionId = @partitionKey")
                .WithParameter("@partitionKey", userSessionId);
        var iterator = _container.GetItemQueryIterator<PensionsRetrievalRecord>(query);
        return await iterator.ReadNextAsync();
    }

    private static PensionsRetrievalRecord CreateRecord(PensionRetrievalPayload payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payload.UserSessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload.Iss);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload.PeisId);

        return new PensionsRetrievalRecord
        {
            Id = Guid.NewGuid().ToString(),
            Iss = payload.Iss,
            UserSessionId = payload.UserSessionId,
            PeisId = payload.PeisId,
            JobStartTimestamp = DateTime.UtcNow
        };
    }
}
