using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TransferFlow.Infrastructure.Messaging.Sqs;

internal sealed class SqsConsumerBackgroundService(
    ILogger<SqsConsumerBackgroundService> logger,
    IServiceScopeFactory serviceScopeFactory,
    IAmazonSQS sqs,
    IOptions<SqsOptions> options)
    : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var queueUrlResponse =
            await sqs.GetQueueUrlAsync(
                options.Value.QueueName,
                stoppingToken);

        var queueUrl = queueUrlResponse.QueueUrl;

        while (!stoppingToken.IsCancellationRequested)
        {
            var response =
                await sqs.ReceiveMessageAsync(
                    new ReceiveMessageRequest
                    {
                        QueueUrl = queueUrl,

                        MaxNumberOfMessages = 1,

                        WaitTimeSeconds = 10,

                        MessageAttributeNames = ["All"]
                    },
                    stoppingToken);

            if (response is null || response.Messages is null || response.Messages.Count == 0)
            {
                continue;
            }

            foreach (var message in response.Messages)
            {
                var correlationId = TryGetCorrelationId(message);
                var applicationMessageId = TryGetMessageId(message);

                using var logScope =
                    logger.BeginScope(
                        new Dictionary<string, object?>
                        {
                            ["CorrelationId"] =
                                correlationId,

                            ["MessageId"] =
                                applicationMessageId,

                            ["SqsMessageId"] =
                                message.MessageId
                        });
                try
                {
                    await using var scope =
                        _serviceScopeFactory.CreateAsyncScope();

                    var processor =
                        scope.ServiceProvider
                            .GetRequiredService<SqsMessageProcessor>();

                    logger.LogInformation(
                        "Processing SQS message.");

                    await processor.ProcessAsync(
                        message,
                        stoppingToken);

                    logger.LogInformation(
                        "SQS message processed successfully.");

                    await sqs.DeleteMessageAsync(
                        queueUrl,
                        message.ReceiptHandle,
                        stoppingToken);

                    logger.LogInformation(
                        "SQS message acknowledged.");
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogError(
                        ex,
                        "Failed to process SQS message.");
                }
            }
        }
    }

    private static Guid? TryGetCorrelationId(
        Message message)
    {
        if (!message.MessageAttributes.TryGetValue(
                "correlation-id",
                out var attribute))
        {
            return null;
        }

        return Guid.TryParse(
            attribute.StringValue,
            out var correlationId)
                ? correlationId
                : null;
    }

    private static Guid? TryGetMessageId(
        Message message)
    {
        if (!message.MessageAttributes.TryGetValue(
                "message-id",
                out var attribute))
        {
            return null;
        }

        return Guid.TryParse(
            attribute.StringValue,
            out var messageId)
                ? messageId
                : null;
    }
}
