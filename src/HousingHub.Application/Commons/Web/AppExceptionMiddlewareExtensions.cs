using Microsoft.AspNetCore.Builder;
﻿namespace HousingHub.Application.Commons.Web;

public static class AppExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseAppExceptionMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ExceptionHandlingMiddleware>();
    }
}
