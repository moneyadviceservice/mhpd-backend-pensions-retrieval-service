using MhpdCommon.Models.MessageBodyModels;

namespace PensionsRetrievalFunction.Orchestration;

public interface IPeiIntegrationOrchestrator
{
    Task RunAsync(PensionRetrievalPayload payload, string correlationId);
}
