using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using SmartDroneDelivery.Api.Controllers;

namespace SmartDroneDelivery.Api.Security;

public class IntegrationSwagger:IOperationFilter
{
    public void Apply(OpenApiOperation operation,OperationFilterContext context)
    {
        var controller=context.MethodInfo.DeclaringType;
        if(controller!=typeof(ExternalOrdersController) && controller!=typeof(IntegrationKeysController)) return;
        var external=controller==typeof(ExternalOrdersController);
        operation.Security=[new OpenApiSecurityRequirement { [new OpenApiSecurityScheme {Reference=new OpenApiReference {Type=ReferenceType.SecurityScheme,Id=external?"IntegrationKey":"Bearer"}}]=Array.Empty<string>() }];
        operation.Description=external
            ? "LA-156 external v1. X-API-Key only; bearer plus key is 400. Bound active CUSTOMER required. POST requires orders:create; GET requires orders:read. Limits: 60 requests and 10 creates/minute/key, process-local. Secrets appear only on creation; do not blindly retry timed-out creates. See docs/api/sprint-6-api-contract.md section 6."
            : "LA-156 provisioning: active persisted ADMIN bearer only. POST reveals plaintext once; GET metadata never contains the key/hash. DELETE revokes immediately and preserves audit history. Target must be an active CUSTOMER. See docs/api/sprint-6-api-contract.md section 6.";
        foreach(var (status,description) in new[]{("400","Invalid input or ambiguous credentials"),("401","Missing, invalid, revoked or expired credentials"),("403","Role, scope or account access denied"),("404","Unknown or foreign resource"),("409","Current resource state conflicts")})
            operation.Responses.TryAdd(status,new OpenApiResponse {Description=description});
        if(external) operation.Responses["429"]=new OpenApiResponse {Description="Key rate limit exceeded",Headers=new Dictionary<string,OpenApiHeader>{{"Retry-After",new(){Description="Seconds until eligible",Schema=new(){Type="integer",Minimum=1}}}}};
    }
}
