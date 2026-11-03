using System.Security.Claims;

namespace MappingDemo.Api.Services;

public interface IDeliveryResendAuthorizer
{
    bool IsAuthorized(ClaimsPrincipal user, string? operatorId);
}

public sealed class DeliveryResendAuthorizer(IConfiguration configuration)
    : IDeliveryResendAuthorizer
{
    public bool IsAuthorized(ClaimsPrincipal user, string? operatorId)
    {
        return configuration.GetValue(
                   "ApiDelivery:AllowExplicitResend",
                   false) &&
               !string.IsNullOrWhiteSpace(operatorId);
    }
}
