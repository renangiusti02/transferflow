using Amazon;
using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TransferFlow.Application.Messaging.Projections;

namespace TransferFlow.Infrastructure.Messaging.Projections.DynamoDb;

public static class DynamoDbProjectionDependencyInjection
{
    public static IServiceCollection AddDynamoDbProjection(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<DynamoDbProjectionOptions>()
            .Bind(
                configuration.GetSection(
                    DynamoDbProjectionOptions.SectionName))
            .Validate(
                options =>
                    !string.IsNullOrWhiteSpace(options.TableName),
                "DynamoDB table name is required.")
            .Validate(
                options =>
                    !string.IsNullOrWhiteSpace(options.Region),
                "AWS region is required.")
            .ValidateOnStart();

        services.AddSingleton<IAmazonDynamoDB>(
            serviceProvider =>
            {
                var options =
                    serviceProvider
                        .GetRequiredService<
                            IOptions<DynamoDbProjectionOptions>>()
                        .Value;

                if (!string.IsNullOrWhiteSpace(
                    options.ServiceUrl))
                {
                    var config =
                        new AmazonDynamoDBConfig
                        {
                            ServiceURL =
                                options.ServiceUrl,

                            AuthenticationRegion =
                                options.Region
                        };

                    var credentials =
                        new BasicAWSCredentials(
                            "test",
                            "test");

                    return new AmazonDynamoDBClient(
                        credentials,
                        config);
                }

                var awsConfig =
                    new AmazonDynamoDBConfig
                    {
                        RegionEndpoint =
                            RegionEndpoint.GetBySystemName(
                                options.Region)
                    };

                return new AmazonDynamoDBClient(
                    awsConfig);
            });

        services.AddScoped<
            IWalletActivityProjection,
            DynamoDbWalletActivityProjection>();

        return services;
    }
}
