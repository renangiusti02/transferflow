using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TransferFlow.Api.Health;

internal static class HealthCheckResponseWriter
{
    public static Task WriteAsync(
        HttpContext context,
        HealthReport report)
    {
        context.Response.ContentType =
            "application/json; charset=utf-8";

        var response = new
        {
            status = report.Status.ToString(),

            checks = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new
                {
                    status =
                        entry.Value.Status.ToString(),

                    duration =
                        entry.Value.Duration.TotalMilliseconds
                })
        };

        return context.Response.WriteAsJsonAsync(
            response);
    }
}
