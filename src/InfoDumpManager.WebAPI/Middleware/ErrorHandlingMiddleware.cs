using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace InfoDumpManager.WebAPI.Middleware;

public sealed class ErrorHandlingMiddleware
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly RequestDelegate _next;

    public ErrorHandlingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException validationException)
        {
            Log.Warning(validationException, "Validation failed for {Path}", context.Request.Path);
            await WriteValidationProblemDetailsAsync(context, validationException);
        }
        catch (System.Security.Authentication.AuthenticationException authException)
        {
            Log.Warning(authException, "Authentication failure for {Path}", context.Request.Path);
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            context.Response.ContentType = "application/problem+json";

            var problemDetails = new ProblemDetails
            {
                Status = (int)HttpStatusCode.Unauthorized,
                Title = "Authentication required.",
                Detail = authException.Message,
                Instance = context.Request.Path
            };

            var payload = JsonSerializer.Serialize(problemDetails, SerializerOptions);
            await context.Response.WriteAsync(payload);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Unhandled exception while processing {Path}", context.Request.Path);
            await WriteProblemDetailsAsync(context, exception);
        }
    }

    private static Task WriteValidationProblemDetailsAsync(HttpContext context, ValidationException exception)
    {
        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;

        var errors = exception.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray());

        var problemDetails = new ValidationProblemDetails(errors)
        {
            Status = (int)HttpStatusCode.BadRequest,
            Title = "One or more validation errors occurred.",
            Instance = context.Request.Path
        };

        var payload = JsonSerializer.Serialize(problemDetails, SerializerOptions);
        return context.Response.WriteAsync(payload);
    }

    private static Task WriteProblemDetailsAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        var problemDetails = new ProblemDetails
        {
            Status = context.Response.StatusCode,
            Title = "An unexpected error occurred.",
            Detail = exception.Message,
            Instance = context.Request.Path
        };

        var payload = JsonSerializer.Serialize(problemDetails, SerializerOptions);
        return context.Response.WriteAsync(payload);
    }
}
