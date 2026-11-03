using System.Security.Claims;

namespace MappingDemo.Api.Services;

public interface IDeliveryOperationsAuthorizer
{
    bool IsAuthorized(ClaimsPrincipal user, string? operatorId);
}

public sealed class DeliveryOperationsAuthorizer(IConfiguration configuration)
    : IDeliveryOperationsAuthorizer
{
    public bool IsAuthorized(ClaimsPrincipal user, string? operatorId) =>
        configuration.GetValue("ApiDelivery:AllowOperationsControl", false) &&
        !string.IsNullOrWhiteSpace(operatorId);
}
