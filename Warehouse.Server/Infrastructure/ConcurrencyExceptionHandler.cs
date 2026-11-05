using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Warehouse.Application.Common.Concurrency;

namespace Warehouse.Server.Infrastructure;

internal sealed class ConcurrencyExceptionHandler(
    ILogger<ConcurrencyExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // A serializable inventory transaction may lose a deadlock race; its changes are rolled back.
        var deadlock = false;
        for (Exception? current = exception; current is not null; current = current.InnerException)
            if (current is SqlException { Number: 1205 }) deadlock = true;
        if (exception is not DbUpdateConcurrencyException && !deadlock) return false;

        logger.LogWarning(
            exception,
            "A concurrent update was rejected for {RequestMethod} {RequestPath}. TraceId: {TraceIdentifier}",
            httpContext.Request.Method,
            httpContext.Request.Path,
            httpContext.TraceIdentifier);

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "تداخل در ویرایش اطلاعات",
            Detail = ConcurrencyMessages.Conflict,
            Instance = httpContext.Request.Path
        };
        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }
}
