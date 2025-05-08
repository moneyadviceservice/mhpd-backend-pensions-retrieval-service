using System.Net;
using MhpdCommon.Constants;
using MhpdCommon.Extensions;
using MhpdCommon.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using PensionsRetrievalFunction.Models;
using PensionsRetrievalFunction.Repository;

namespace PensionsRetrievalFunction;


public class RetrievalRecordFunction(ILogger<RetrievalRecordFunction> logger, IPensionRetrievalRepository repository, IIdValidator validator)
{
    [Function("GetRetrievalRecords")]
    [OpenApiOperation(operationId: "get-pensions-retrieval-record-for-pension-owner-session",
        Summary = "Get Pensions Retrieval Record",
        Description = "Get the pensions retrieval record that contains the information on the state of a process to retrieve the pensions data for a user session from the PDP ecosystem")]
    [OpenApiParameter(
        HeaderConstants.UserSessionId,
        In = ParameterLocation.Header, 
        Description = "The unique id of pension owner session as issued by the requesting system",
        Required = true)]
    [OpenApiParameter(
        HeaderConstants.CorrelationId,
        In = ParameterLocation.Header,
        Description = "An Id with which to group all logging statements made during a single session",
        Required = false)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "The OK response message containing pension retrieval record.")]
    [OpenApiResponseWithoutBody(HttpStatusCode.BadRequest, Description = "BadRequest")]
    [OpenApiResponseWithoutBody(HttpStatusCode.Unauthorized, Description = "Unauthorized")]
    [OpenApiResponseWithoutBody(HttpStatusCode.Forbidden, Description = "Forbidden")]
    [OpenApiResponseWithoutBody(HttpStatusCode.InternalServerError, Description = "Internal Server Error")]
    [OpenApiResponseWithoutBody(HttpStatusCode.BadGateway, Description = "BadGateway")]
    [OpenApiResponseWithoutBody(HttpStatusCode.ServiceUnavailable, Description = "Service Unavailable")]
    [OpenApiResponseWithoutBody(HttpStatusCode.GatewayTimeout, Description = "Gateway Timeout")]
    public async Task<IActionResult> GetAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "pensions-retrieval-records")] HttpRequest req)
    {
        return await ProcessRetrievalRecords(req, repository.GetRetrievalRecordAsync);
    }

    [Function("DeleteRetrievalRecords")]
    [OpenApiOperation(operationId: "delete-pensions-retrieval-records-id",
        Summary = "Delete Pensions Retrieval Record",
        Description = "Deletes the given pension retrieval record id.")]
    [OpenApiParameter(
        HeaderConstants.UserSessionId,
        In = ParameterLocation.Header,
        Description = "The unique id of pension owner session as issued by the requesting system",
        Required = true)]
    [OpenApiParameter(
        HeaderConstants.CorrelationId,
        In = ParameterLocation.Header,
        Description = "An Id with which to group all logging statements made during a single session",
        Required = false)]
    [OpenApiResponseWithoutBody(HttpStatusCode.NoContent, Description = "No Content")]
    [OpenApiResponseWithoutBody(HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<IActionResult> DeleteAsync([HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "pensions-retrieval-records")] HttpRequest req)
    {
        return await ProcessRetrievalRecords(req, repository.DeleteRetrievalRecordsAsync);
    }

    private async Task<IActionResult> ProcessRetrievalRecords<T>(HttpRequest req, Func<string, Task<T>> processor)
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

        logger.LogRequest($"User session Id: {userSessionId}");

        if (!validator.IsValidGuid(userSessionId))
        {
            logger.LogError("Unable to service request for sessionId [{sessionId}]: {reason}", userSessionId, Constants.ResponseType.InvalidSessionId);
            return new BadRequestObjectResult(Constants.ResponseType.InvalidSessionId);
        }

        var record = await processor(userSessionId);
        logger.LogResponse(record);

        return EqualityComparer<T>.Default.Equals(record, default) ? new OkResult() : new OkObjectResult(record);
    }
}
