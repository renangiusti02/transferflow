using Amazon;
using Amazon.Runtime;
using Amazon.SQS;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TransferFlow.Infrastructure.Messaging.Sqs;

namespace TransferFlow.Infrastructure.Messaging.Outbox;

public static class OutboxDependencyInjection
{
    public static IServiceCollection AddOutboxProcessing(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<SqsOptions>()
            .Bind(configuration.GetSection(SqsOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.QueueName),
                "SQS queue name is required.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Region),
                "AWS region is required.")
            .ValidateOnStart();

        services
            .AddSingleton<IAmazonSQS>(serviceProvider =>
        {
            var options =
                serviceProvider
                    .GetRequiredService<IOptions<SqsOptions>>()
                    .Value;

            if (!string.IsNullOrWhiteSpace(options.ServiceUrl))
            {
                var config = new AmazonSQSConfig
                {
                    ServiceURL = options.ServiceUrl,
                    AuthenticationRegion = options.Region
                };

                var credentials =
                    new BasicAWSCredentials("test", "test");

                return new AmazonSQSClient(
                    credentials,
                    config);
            }

            var awsConfig = new AmazonSQSConfig
            {
                RegionEndpoint =
                    RegionEndpoint.GetBySystemName(
                        options.Region)
            };

            return new AmazonSQSClient(awsConfig);
        });

        services
            .AddScoped<IOutboxPublisher, SqsOutboxPublisher>();

        services
            .AddScoped<OutboxProcessor>();

        services
            .AddHostedService<OutboxBackgroundService>();

        return services;
    }
}
