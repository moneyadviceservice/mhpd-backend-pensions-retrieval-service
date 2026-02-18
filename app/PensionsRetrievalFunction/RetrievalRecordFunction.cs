using MhpdCommon.Constants;
using MhpdCommon.Extensions;
using MhpdCommon.OpenApi;
using MhpdCommon.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using PensionsRetrievalFunction.Models;
using PensionsRetrievalFunction.Orchestration;
using PensionsRetrievalFunction.Repository;
using System.Net;

namespace PensionsRetrievalFunction;

public class RetrievalRecordFunction(ILogger<RetrievalRecordFunction> logger, IPensionRetrievalRepository repository, IIdValidator validator, IPeiIntegrationOrchestrator peiIntegrationOrchestrator)
{
    [Function("PostRetrievalRecords")]
    [OpenApiOperation(operationId: "post-pensions-retrieval-record-for-pension-owner-session",
        Summary = "Post Pensions Retrieval Record",
        Description = "Post the pensions retrieval record. This will schedule the messages to be handled at the desired times to poll for new retrieval services")]
    [PensionDataOpenApi]
    [OpenApiResponseWithBody(HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "The OK response message containing pension retrieval record. Indicating the retrival has been queued.")]
    public async Task<IActionResult> PostAsync([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "pensions-retrieval-records")] HttpRequest req, [Microsoft.Azure.Functions.Worker.Http.FromBody] MhpdCommon.Models.MessageBodyModels.PensionRetrievalPayload payload)
    {
        return await ProcessRetrievalRecords(req, "POST", (userSessionId) => peiIntegrationOrchestrator.ScheduleMessagesAsync(payload, userSessionId));
    }


    [Function("GetRetrievalRecords")]
    [OpenApiOperation(operationId: "get-pensions-retrieval-record-for-pension-owner-session",
        Summary = "Get Pensions Retrieval Record",
        Description = "Get the pensions retrieval record that contains the information on the state of a process to retrieve the pensions data for a user session from the PDP ecosystem")]
    [PensionDataOpenApi]
    [OpenApiResponseWithBody(HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "The OK response message containing pension retrieval record.")]
    public async Task<IActionResult> GetAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "pensions-retrieval-records")] HttpRequest req)
    {
        return await ProcessRetrievalRecords(req, "GET", repository.GetRetrievalRecordAsync);
    }

    [Function("DeleteRetrievalRecords")]
    [OpenApiOperation(operationId: "delete-pensions-retrieval-records-id",
        Summary = "Delete Pensions Retrieval Record",
        Description = "Deletes the given pension retrieval record id.")]
    [PensionDataOpenApi(false)]
    [OpenApiResponseWithoutBody(HttpStatusCode.NoContent, Description = "No Content")]
    [OpenApiResponseWithoutBody(HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<IActionResult> DeleteAsync([HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "pensions-retrieval-records")] HttpRequest req)
    {
        return await ProcessRetrievalRecords(req, "DELETE", async userSessionId =>
        {
            await repository.DeleteRetrievalRecordsAsync(userSessionId);
            return default(int);
        });
    }

    private async Task<IActionResult> ProcessRetrievalRecords<T>(HttpRequest req, string source, Func<string, Task<T>> processor)
    {
        var correlationId = req.Headers[HeaderConstants.CorrelationId].ToString();
        if (string.IsNullOrEmpty(correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
        }

        if (!validator.IsValidGuid(correlationId))
        {
            return new BadRequestObjectResult(Constants.ResponseType.InvalidCorrelationId);
        }

        var userSessionId = req.Headers[HeaderConstants.UserSessionId].ToString();

        using var scope = logger.BeginCorrelationScope(correlationId, Constants.LogSource.Http);

        logger.LogRequestReceived($"{source} for user session Id: {userSessionId}");

        if (!validator.IsValidGuid(userSessionId))
        {
            logger.LogError("Unable to service request for sessionId [{SessionId}]: {Reason}", userSessionId, Constants.ResponseType.InvalidSessionId);
            return new BadRequestObjectResult(Constants.ResponseType.InvalidSessionId);
        }

        var record = await processor(userSessionId);
        logger.LogResponseSent(record);

        return EqualityComparer<T>.Default.Equals(record, default) ? new OkResult() : new OkObjectResult(record);
    }
}
