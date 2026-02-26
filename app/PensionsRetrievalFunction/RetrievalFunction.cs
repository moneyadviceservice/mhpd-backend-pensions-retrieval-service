using Azure.Messaging.ServiceBus;
using MhpdCommon.Extensions;
using MhpdCommon.Models.MessageBodyModels;
using MhpdCommon.Utils;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using PensionsRetrievalFunction.Models;
using PensionsRetrievalFunction.Orchestration;
using System.Text;

namespace PensionsRetrievalFunction;

public class RetrievalFunction(ILogger<RetrievalFunction> logger,
    IIdValidator idValidator,
    IMessageParser messageParser,
    IPeiIntegrationOrchestrator orchestrator)
{
    [Function(nameof(RetrievalFunction))]
    public async Task Run(
        [ServiceBusTrigger("%CommonServiceBusConfiguration:InboundQueue%", Connection = "ServiceBusConnectionstring", IsBatched = true)]
        ServiceBusReceivedMessage[] messages,
        ServiceBusMessageActions messageActions)
    {
        var messageTasks = messages.Select(m => HandleMessageAsync(m, messageActions)).ToList();
        await Task.WhenAll(messageTasks);
    }

    private async Task HandleMessageAsync(ServiceBusReceivedMessage message, ServiceBusMessageActions messageActions)
    {
        await Task.Yield();
        if (!idValidator.IsValidGuid(message.CorrelationId))
        {
            logger.LogCritical("Missing or Invalid correlationId: {CorrelationId}", message.CorrelationId);
            await messageActions.DeadLetterMessageAsync(message, deadLetterReason: $"Missing or Invalid correlationId: {message.CorrelationId}");
            return;
        }

        using var scope = logger.BeginCorrelationScope(message.CorrelationId, Constants.LogSource.Queue);
        LogRequestMesage(message);

        try
        {
            var payload = ExtractAndValidateMessagePayload(message);
            await orchestrator.RunAsync(payload!, message.CorrelationId);
            await messageActions.CompleteMessageAsync(message);
        }
        catch (Exception error)
        {
            logger.LogCritical(error, "{Message}", error.Message);
            await messageActions.DeadLetterMessageAsync(message, deadLetterReason: error.Message);
        }
    }

    private PensionRetrievalMessagePayload ExtractAndValidateMessagePayload(ServiceBusReceivedMessage message)
    {
        var messageBody = Encoding.UTF8.GetString(message.Body);
        PensionRetrievalMessagePayload? payload;

        try
        {
            payload = messageParser.ToPensionRetrievalMessagePayload(messageBody);
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
        var enqueudTimespan = DateTimeOffset.UtcNow - receivedMessage.EnqueuedTime;
        if (enqueudTimespan.TotalSeconds > 5)
        {
            logger.LogWarning("Message with MessageId: {MessageId}, CorrelationId: {CorrelationId} has been in the queue for {EnqueudTimespan}.", receivedMessage.MessageId, receivedMessage.CorrelationId, enqueudTimespan);
        }

        var logMessage = $"Message Received - CorrelationId:[{receivedMessage.CorrelationId}], MessageId: [{receivedMessage.MessageId}], ContentType: [{receivedMessage.ContentType}] {Environment.NewLine}";
        logger.LogWarning("Message Details : {Details} Body: {Body}", logMessage, receivedMessage.Body);
    }
}