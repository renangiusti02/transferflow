namespace TransferFlow.Api.Middleware;

internal sealed class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger)
{
    private const string HeaderName =
        "X-Correlation-ID";

    public async Task InvokeAsync(
        HttpContext context)
    {
        var correlationId =
            TryGetCorrelationId(context)
            ?? Guid.NewGuid().ToString();

        context.TraceIdentifier =
            correlationId;

        context.Response.Headers[HeaderName] =
            correlationId;

        using (logger.BeginScope(
            new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId
            }
        ))
        {
            await next(context);
        }
    }

    private static string? TryGetCorrelationId(
        HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(
            HeaderName, out var values))
        {
            return null;
        }

        var value = values.FirstOrDefault();

        return Guid.TryParse(value, out var parsed)
            ? parsed.ToString()
            : null;
    }
}
