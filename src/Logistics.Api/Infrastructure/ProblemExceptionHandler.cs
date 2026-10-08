using Logistics.Application.Common;
using Logistics.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Logistics.Api.Infrastructure;

/// <summary>Turns known exceptions into RFC 7807 problem responses. Anything else is a 500 with no internals leaked.</summary>
internal sealed class ProblemExceptionHandler(IProblemDetailsService problemDetails, ILogger<ProblemExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, exception.Message),
            DomainException => (StatusCodes.Status422UnprocessableEntity, exception.Message),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict,
                "The shipment was changed by someone else. Reload and try again."),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred."),
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);

        context.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = status, Title = title },
        });
    }
}
