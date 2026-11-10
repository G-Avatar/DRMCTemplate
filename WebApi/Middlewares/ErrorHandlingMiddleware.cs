using Application.Common.Exceptions;
using Application.Common.Models;
using System.Net;

namespace WebApi.Middlewares;

public class ErrorHandlingMiddleware(
    RequestDelegate next,
    ILogger<ErrorHandlingMiddleware> logger,
    IHostEnvironment env)
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger = logger;
    private readonly IHostEnvironment _env = env;

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AppException ex) when (!context.Response.HasStarted)
        {
            _logger.LogWarning(
                ex,
                "Request failed with status {StatusCode}. Message: {Message}, TraceId: {TraceId}",
                ex.StatusCode,
                ex.Message,
                context.TraceIdentifier
            );

            await WriteResponse(context, ex.StatusCode, new ResponseDto<object>
            {
                Message = ex.Message,
                Errors = (ex as ValidationException)?.Errors
            });
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            _logger.LogError(
                ex,
                "Unhandled exception occurred. TraceId: {TraceId}, Path: {Path}",
                context.TraceIdentifier,
                context.Request.Path
            );

            await WriteResponse(context, (int)HttpStatusCode.InternalServerError, new ResponseDto<object>
            {
                Message = "Something went wrong.",
                Data = _env.IsDevelopment()
                    ? ex.ToString()
                    : null
            });
        }
    }

    private static Task WriteResponse(HttpContext context, int statusCode, ResponseDto<object> response)
    {
        context.Response.Clear();
        context.Response.StatusCode = statusCode;

        // WriteAsJsonAsync sets the application/json content type itself
        return context.Response.WriteAsJsonAsync(response, context.RequestAborted);
    }
}
