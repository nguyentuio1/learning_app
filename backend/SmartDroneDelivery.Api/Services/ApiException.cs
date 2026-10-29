namespace SmartDroneDelivery.Api.Services;

/// <summary>An error that is returned to the client as an RFC 7807 problem body.</summary>
public class ApiException : Exception
{
    public int StatusCode { get; }

    /// <summary>Messages per field (camelCase), only used for 400 validation errors.</summary>
    public IDictionary<string, string[]>? Errors { get; }

    public ApiException(int statusCode, string detail, IDictionary<string, string[]>? errors = null) : base(detail)
    {
        StatusCode = statusCode;
        Errors = errors;
    }
}
