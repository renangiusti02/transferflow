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
                try
                {
                    await using var scope =
                        _serviceScopeFactory.CreateAsyncScope();

                    var processor =
                        scope.ServiceProvider
                            .GetRequiredService<SqsMessageProcessor>();

                    await processor.ProcessAsync(
                        message,
                        stoppingToken);

                    await sqs.DeleteMessageAsync(
                        queueUrl,
                        message.ReceiptHandle,
                        stoppingToken);
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
                        "Failed to process SQS message {MessageId}. " +
                        "The message will not be deleted.",
                        message.MessageId);
                }
            }
        }
    }
}
