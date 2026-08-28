using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using HousingHub.Core.CustomResponses;
using HousingHub.Application.Commons.Exceptions;

namespace HousingHub.Application.Commons.Web;

public class ExceptionHandlingMiddleware : IMiddleware
{
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(ILogger<ExceptionHandlingMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException e)
        {
            // A rejected form is the system working, not failing. Logged at Warning so
            // it stays visible locally without becoming an error report: Sentry's event
            // threshold is LogError, and on a 5,000-event monthly budget a few users
            // mistyping an email would consume the quota that real bugs need.
            _logger.LogWarning(e, "Validation failed for {Path}", context.Request.Path);
            await HandleExceptionAsync(context, e);
        }
        catch (Exception e)
        {
            // Error level is deliberate — this is what causes the event to reach
            // Sentry. See the UseSentry configuration in Program.cs.
            _logger.LogError(e, e.Message);
            await HandleExceptionAsync(context, e);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext httpContext, Exception exception)
    {
        var statusCode = GetStatusCode(exception);

        // Validation failures are written by us and safe to show. Anything else is an
        // unhandled exception whose message can carry table names, driver detail or
        // other internals — the full exception is already logged above.
        var message = exception is ValidationException
            ? exception.Message
            : ResponseMessages.UnexpectedError;

        var response = new BaseErrorResponse(GetErrors(exception), statusCode.ToString(), message);
        httpContext.Response.ContentType = "application/json";
        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsync(JsonSerializer.Serialize(response));
    }

    private static int GetStatusCode(Exception exception) =>
        exception switch
        {
            ValidationException => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        };

    private static HashSet<string> GetErrors(Exception exception)
    {
        HashSet<string> errorHash = new();
        var counter = 0;
        if (exception is ValidationException validationException)
        {
            errorHash = validationException.Errors.Select(x => $"{x.ErrorMessage}").ToHashSet<string>();
        }

        if (errorHash.Count == 1)
        {
            return errorHash;
        }

        return errorHash.Select(x => $"{++counter}. {x}").ToHashSet<string>();
    }

}
