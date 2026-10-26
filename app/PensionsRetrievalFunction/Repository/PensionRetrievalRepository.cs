using MhpdCommon.Models.MessageBodyModels;
using MhpdCommon.Models.MHPDModels;
using MhpdCommon.Repository;
using Microsoft.Extensions.Logging;

namespace PensionsRetrievalFunction.Repository;

public class PensionRetrievalRepository(IHashRedisRepository<PensionsRetrievalRecord> pensionsRetrievalRecordRepository) 
    : IPensionRetrievalRepository
{
    public async Task<PensionsRetrievalRecord?> CreateRecordIfNotExistsAsync(PensionRetrievalPayload payload)
    {
        var result = await pensionsRetrievalRecordRepository.GetByUserSessionIdAsync(payload.UserSessionId!);
        if (result == null)
        {
            var record = CreateRecord(payload);

            await pensionsRetrievalRecordRepository.UpsertItemAsync(record);

            return record;
        }

        return null;
    }

    public Task UpdatePensionsRetrievalRecordAsync(PensionsRetrievalRecord record)
    {
        return pensionsRetrievalRecordRepository.UpsertItemAsync(record);
    }

    public async Task<PensionsRetrievalRecord?> GetRetrievalRecordAsync(string userSessionId)
    {
        return await pensionsRetrievalRecordRepository.GetByUserSessionIdAsync(userSessionId);
    }

    public Task DeleteRetrievalRecordsAsync(string userSessionId)
    {
        return pensionsRetrievalRecordRepository.DeleteByIdUserSessionIdAsync(userSessionId);
    }

    private static PensionsRetrievalRecord CreateRecord(PensionRetrievalPayload payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payload.UserSessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload.Iss);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload.PeisId);

        return new PensionsRetrievalRecord
        {
            Iss = payload.Iss,
            UserSessionId = payload.UserSessionId,
            PeisId = payload.PeisId,
            JobStartTimestamp = DateTime.UtcNow
        };
    }
}
