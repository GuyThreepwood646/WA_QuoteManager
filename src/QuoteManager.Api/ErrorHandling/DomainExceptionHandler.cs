using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuoteManager.Domain.Common;

namespace QuoteManager.Api.ErrorHandling;

/// <summary>
/// AD-8: maps typed domain exceptions to RFC 9457 problem details carrying a stable machine
/// <c>code</c> and the active trace id, so endpoint handlers never need a <c>try</c>/<c>catch</c>
/// for a domain rule violation.
/// </summary>
public sealed class DomainExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, code, detail) = exception switch
        {
            QuoteTransitionNotAllowedException e => (
                e.BlockedByRole ? StatusCodes.Status403Forbidden : StatusCodes.Status409Conflict,
                e.Code,
                e.Message),
            QuoteNotFoundInRequestException e => (StatusCodes.Status404NotFound, e.Code, e.Message),
            DomainException e => (StatusCodes.Status409Conflict, e.Code, e.Message),
            DbUpdateConcurrencyException => (
                StatusCodes.Status409Conflict,
                "quote.concurrent_modification",
                "The record has changed since it was read."),
            _ => (0, null, null),
        };

        if (statusCode == 0)
        {
            return false;
        }

        httpContext.Response.StatusCode = statusCode;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = TitleFor(statusCode),
                Detail = detail,
                Extensions =
                {
                    ["code"] = code,
                    ["traceId"] = Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier,
                },
            },
        });
    }

    private static string TitleFor(int statusCode) => statusCode switch
    {
        StatusCodes.Status403Forbidden => "Not permitted",
        StatusCodes.Status404NotFound => "Not found",
        _ => "The request could not be completed",
    };
}
