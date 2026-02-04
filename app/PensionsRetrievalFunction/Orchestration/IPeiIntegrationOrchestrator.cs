using MhpdCommon.Models.MessageBodyModels;
using MhpdCommon.Models.MHPDModels;

namespace PensionsRetrievalFunction.Orchestration;

public interface IPeiIntegrationOrchestrator
{
    Task<PensionsRetrievalRecord?> ScheduleMessagesAsync(PensionRetrievalPayload payload, string correlationId);

    Task RunAsync(PensionRetrievalMessagePayload payload, string correlationId);
}
