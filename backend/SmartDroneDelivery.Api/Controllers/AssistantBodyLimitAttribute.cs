using Microsoft.AspNetCore.Mvc.Filters;
using SmartDroneDelivery.Api.Middleware;

namespace SmartDroneDelivery.Api.Controllers;

/// <summary>Bounds known and chunked request bodies before JSON model binding, including TestServer.</summary>
public sealed class AssistantBodyLimitAttribute : Attribute, IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var http = context.HttpContext; const int limit = 32 * 1024;
        http.Response.Headers.CacheControl = "no-store";
        if (http.Request.ContentLength > limit) { await ApiProblemDetails.WriteAsync(http, 413, "Request body is too large."); return; }
        var original = http.Request.Body; var bytes = new byte[limit + 1];
        var read = await original.ReadAtLeastAsync(bytes, bytes.Length, false, http.RequestAborted);
        if (read > limit) { await ApiProblemDetails.WriteAsync(http, 413, "Request body is too large."); return; }
        using var bounded = new MemoryStream(bytes, 0, read, writable: false);
        http.Request.Body = bounded;
        try { await next(); }
        finally { http.Request.Body = original; }
    }
}
