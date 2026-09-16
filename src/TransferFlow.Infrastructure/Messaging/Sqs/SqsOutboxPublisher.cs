using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Options;
using TransferFlow.Infrastructure.Messaging.Outbox;

namespace TransferFlow.Infrastructure.Messaging.Sqs;

internal sealed class SqsOutboxPublisher(
    IAmazonSQS _sqs,
    IOptions<SqsOptions> _options) : IOutboxPublisher
{

    public async Task PublishAsync(
        OutboxMessage message,
        CancellationToken cancellationToken)
    {
        var queueUrlResponse =
            await _sqs.GetQueueUrlAsync(
                _options.Value.QueueName,
                cancellationToken);

        var request = new SendMessageRequest
        {
            QueueUrl = queueUrlResponse.QueueUrl,
            MessageBody = message.Payload,

            MessageAttributes =
                new Dictionary<string, MessageAttributeValue>
                {
                    ["message-id"] = new()
                    {
                        DataType = "String",
                        StringValue = message.Id.ToString()
                    },

                    ["event-type"] = new()
                    {
                        DataType = "String",
                        StringValue = message.Type
                    },

                    ["correlation-id"] = new()
                    {
                        DataType = "String",
                        StringValue =
                            message.CorrelationId.ToString()
                    }
                }
        };

        await _sqs.SendMessageAsync(
            request,
            cancellationToken);
    }
}
