namespace SmartDroneDelivery.Api.Security;

/// <summary>
/// Role codes of the five actors. They match the <c>roles.code</c> column and the <c>role</c> claim of the access token.
/// Use them in attributes, for example <c>[Authorize(Roles = AppRoles.Admin)]</c> or
/// <c>[Authorize(Roles = AppRoles.Dispatcher + "," + AppRoles.Admin)]</c>.
/// </summary>
public static class AppRoles
{
    public const string Customer = "CUSTOMER";
    public const string Dispatcher = "DISPATCHER";
    public const string StationOperator = "STATION_OPERATOR";
    public const string Manager = "MANAGER";
    public const string Admin = "ADMIN";

    public static readonly IReadOnlyList<string> All = new[] { Customer, Dispatcher, StationOperator, Manager, Admin };
}
