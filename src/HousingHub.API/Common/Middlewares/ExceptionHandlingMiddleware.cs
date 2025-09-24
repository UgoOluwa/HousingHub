using System.Text.Json;
using HousingHub.Core.CustomResponses;
using HousingHub.Application.Commons.Exceptions;

namespace HousingHub.API.Common.Middlewares;

internal class ExceptionHandlingMiddleware : IMiddleware
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
        catch (Exception e)
        {
            _logger.LogError(e, e.Message);
            await HandleExceptionAsync(context, e);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext httpContext, Exception exception)
    {
        var statusCode = GetStatusCode(exception);
        var response = new BaseErrorResponse(GetErrors(exception), statusCode.ToString(), exception.Message);
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
