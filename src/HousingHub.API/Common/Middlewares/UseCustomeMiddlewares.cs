namespace HousingHub.API.Common.Middlewares;

public static class UseCustomMiddlerwares
{
    public static IApplicationBuilder UseAppExceptionMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ExceptionHandlingMiddleware>();
    }
}
