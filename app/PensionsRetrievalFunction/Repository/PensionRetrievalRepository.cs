using MhpdCommon.Models.Configuration;
using MhpdCommon.Models.MessageBodyModels;
using MhpdCommon.Models.MHPDModels;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

namespace PensionsRetrievalFunction.Repository;

public class PensionRetrievalRepository(IOptions<CosmosBusinessConfiguration> options, CosmosClient client) : IPensionRetrievalRepository
{
    private readonly CosmosBusinessConfiguration _configuration = options.Value;

    public async Task<PensionsRetrievalRecord?> CreateRecordIfNotExistsAsync(PensionRetrievalPayload payload)
    {
        var response = await GetMatchingRecordsAsync(payload.UserSessionId!);
        var container = client.GetContainer(_configuration.DatabaseId, _configuration.PensionsRetrievalContainer);

        if(response.Count == 0)
        {
            var record = CreateRecord(payload);

            var writeResponse = await container.CreateItemAsync(
                item: record,
                partitionKey: new PartitionKey(record.UserSessionId)
            );

            return writeResponse.Resource;
        }

        return null;
    }

    public async Task UpdatePensionsRetrievalRecordAsync(PensionsRetrievalRecord record)
    {
        var container = client.GetContainer(_configuration.DatabaseId, _configuration.PensionsRetrievalContainer);
        await container.ReplaceItemAsync(record, record.Id, new PartitionKey(record.UserSessionId), null, default);
    }

    public async Task<PensionsRetrievalRecord?> GetRetrievalRecordAsync(string userSessionId)
    {
        var response = await GetMatchingRecordsAsync(userSessionId);
        return response.SingleOrDefault();
    }

    public async Task<int> DeleteRetrievalRecordsAsync(string userSessionId)
    {
        var container = client.GetContainer(_configuration.DatabaseId, _configuration.PensionsRetrievalContainer);
        var records = await GetMatchingRecordsAsync(userSessionId);

        foreach (var record in records)
        {
            await container.DeleteItemAsync<RetrievedPensionRecord>(record.Id, new PartitionKey(record.UserSessionId));
        }

        return records.Count;
    }

    private async Task<FeedResponse<PensionsRetrievalRecord>> GetMatchingRecordsAsync(string userSessionId)
    {
        var query = new QueryDefinition("SELECT TOP 1 * FROM c WHERE c.userSessionId = @partitionKey")
                .WithParameter("@partitionKey", userSessionId);

        var container = client.GetContainer(_configuration.DatabaseId, _configuration.PensionsRetrievalContainer);
        var iterator = container.GetItemQueryIterator<PensionsRetrievalRecord>(query);

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
