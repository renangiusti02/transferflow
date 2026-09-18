using Amazon.SQS.Model;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TransferFlow.Application.Messaging.Events;
using TransferFlow.Infrastructure.Persistence;

namespace TransferFlow.Infrastructure.Messaging.Sqs;

internal sealed class SqsMessageProcessor(
    TransferFlowDbContext dbContext)
{
    public async Task ProcessAsync(
        Message message,
        CancellationToken cancellationToken)
    {
        if (!message.MessageAttributes.TryGetValue(
            "message-id",
            out var messageIdAttribute) || !Guid.TryParse(
            messageIdAttribute.StringValue,
            out var messageId))
        {
            throw new InvalidOperationException(
                "SQS message does not contain a valid message-id.");
        }

        var alreadyProcessed =
            await dbContext
                .Set<ProcessedMessage>()
                .AnyAsync(
                    processedMessage =>
                        processedMessage.MessageId == messageId,
                    cancellationToken);

        if (alreadyProcessed)
        {
            return;
        }

        if (!message.MessageAttributes.TryGetValue(
            "event-type",
            out var eventTypeAttribute))
        {
            throw new InvalidOperationException(
                "SQS message does not contain an event-type.");
        }

        if (eventTypeAttribute.StringValue !=
            nameof(TransferCompleted))
        {
            throw new InvalidOperationException(
                $"Unsupported event type: {eventTypeAttribute.StringValue}");
        }

        _ =
            JsonSerializer.Deserialize<TransferCompleted>(
                message.Body)
            ?? throw new InvalidOperationException(
                "Invalid TransferCompleted payload.");

        dbContext
            .Set<ProcessedMessage>()
            .Add(
                new ProcessedMessage(
                    messageId,
                    DateTimeOffset.UtcNow));

        await dbContext.SaveChangesAsync(
            cancellationToken);
    }
}
