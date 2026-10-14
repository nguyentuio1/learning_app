using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Api.Middleware;

/// <summary>Builds the error body described in docs/api/auth-api.md (RFC 7807 problem details).</summary>
public static class ApiProblemDetails
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static object Body(int status, string detail, IDictionary<string, string[]>? errors = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["type"] = $"https://httpstatuses.com/{status}",
            ["title"] = Title(status),
            ["status"] = status,
            ["detail"] = detail
        };
        if (errors is not null)
            body["errors"] = errors;
        return body;
    }

    public static async Task WriteAsync(HttpContext context, int status, string detail)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(Body(status, detail), JsonOptions));
    }

    public static IActionResult FromModelState(ActionContext context)
    {
        var errors = context.ModelState
            .Where(e => e.Value is { Errors.Count: > 0 })
            .ToDictionary(
                e => ToCamelCase(e.Key),
                e => e.Value!.Errors.Select(x => string.IsNullOrEmpty(x.ErrorMessage) ? "Invalid value." : x.ErrorMessage).ToArray());

        return new ObjectResult(Body(StatusCodes.Status400BadRequest, "One or more validation errors occurred.", errors))
        {
            StatusCode = StatusCodes.Status400BadRequest,
            ContentTypes = { "application/problem+json" }
        };
    }

    public static string Title(int status) => status switch
    {
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        409 => "Conflict",
        _ => "Error"
    };

    private static string ToCamelCase(string key) =>
        string.IsNullOrEmpty(key) ? key : char.ToLowerInvariant(key[0]) + key[1..];
}

public class ApiExceptionMiddleware
{
    private readonly RequestDelegate _next;

    public ApiExceptionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ApiException ex) when (!context.Response.HasStarted)
        {
            await ApiProblemDetails.WriteAsync(context, ex.StatusCode, ex.Message);
        }
    }
}
