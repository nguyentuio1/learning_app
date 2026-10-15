namespace SmartDroneDelivery.Api.Services;

/// <summary>An error that is returned to the client as an RFC 7807 problem body.</summary>
public class ApiException : Exception
{
    public int StatusCode { get; }

    public ApiException(int statusCode, string detail) : base(detail)
    {
        StatusCode = statusCode;
    }
}
