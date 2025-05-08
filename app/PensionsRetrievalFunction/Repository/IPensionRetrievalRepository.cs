using MhpdCommon.Models.MessageBodyModels;
using MhpdCommon.Models.MHPDModels;

namespace PensionsRetrievalFunction.Repository;

public interface IPensionRetrievalRepository
{
    Task<PensionsRetrievalRecord?> CreateRecordIfNotExistsAsync(PensionRetrievalPayload payload);

    Task UpdatePensionsRetrievalRecordAsync(PensionsRetrievalRecord record);

    Task<PensionsRetrievalRecord?> GetRetrievalRecordAsync(string userSessionId);

    Task<int> DeleteRetrievalRecordsAsync(string userSessionId);
}
