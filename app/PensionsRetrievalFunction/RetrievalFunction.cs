using Azure.Messaging.ServiceBus;
using MhpdCommon.Extensions;
using MhpdCommon.Models.MessageBodyModels;
using MhpdCommon.Utils;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using PensionsRetrievalFunction.Models;
using PensionsRetrievalFunction.Orchestration;
using System.Text;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.OpenApi.Models;
using System.Net;
using System.Diagnostics.CodeAnalysis;

namespace PensionsRetrievalFunction;

public class RetrievalFunction(ILogger<RetrievalFunction> logger,
    IIdValidator idValidator,
    IMessageParser messageParser,
    IPeiIntegrationOrchestrator orchestrator)
{
    [Function(nameof(RetrievalFunction))]
    public async Task Run(
        [ServiceBusTrigger("%CommonServiceBusConfiguration:InboundQueue%", Connection = "ServiceBusConnectionstring")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions)
    {
        if (!idValidator.IsValidGuid(message.CorrelationId))
        {
            logger.LogCritical("Missing or Invalid correlationId: {correlationId}", message.CorrelationId);
            await messageActions.DeadLetterMessageAsync(message, deadLetterReason: $"Missing or Invalid correlationId: {message.CorrelationId}");
            return;
        }

        using var scope = logger.BeginCorrelationScope(message.CorrelationId, Constants.LogSource.Queue);
        LogRequestMesage(message);

        try
        {
            var payload = ExtractAndValidateMessagePayload(message);

            // Release the lock on the message
            await messageActions.CompleteMessageAsync(message);

            await orchestrator.RunAsync(payload, message.CorrelationId);
        }
        catch (Exception error)
        {
            logger.LogCritical(error, "{message}", error.Message);
            await messageActions.DeadLetterMessageAsync(message, deadLetterReason: error.Message);
        }
    }

    private PensionRetrievalPayload ExtractAndValidateMessagePayload(ServiceBusReceivedMessage message)
    {
        var messageBody = Encoding.UTF8.GetString(message.Body);
        PensionRetrievalPayload? payload;

        try
        {
            payload = messageParser.ToPensionRetrievalPayload(messageBody);
        }
        catch (AggregateException error)
        {
            var builder = new StringBuilder(Constants.ResponseType.InvalidPayloadResponse);
            builder.AppendLine();
            foreach (var ex in error.InnerExceptions)
            {
                builder.AppendLine(ex.Message);
            }

            var logMessage = builder.ToString();
            throw new InvalidDataException(logMessage, error);
        }

        return payload!;
    }

    private void LogRequestMesage(ServiceBusReceivedMessage receivedMessage)
    {
        var logMessage = $"Message Received - CorrelationId:[{receivedMessage.CorrelationId}], " +
            $"MessageId: [{receivedMessage.MessageId}], ContentType: [{receivedMessage.ContentType}] {Environment.NewLine}";
        logger.LogWarning("Message Details : {details} Body: {body}", logMessage, receivedMessage.Body);
    }
}

[ExcludeFromCodeCoverage]
public static class RetrievalFunctionOpenApiSpec
{
    private const string Tag = "items";

    [Function("GetItem")]
    [OpenApiOperation(operationId: "GetItem", tags: [Tag])]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(string))]
    public static HttpResponseData Run([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.WriteString("Hello, OpenAPI!");
        return response;
    }
}