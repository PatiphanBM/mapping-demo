using System.Security.Claims;
using MappingDemo.Shared.Contracts;

namespace MappingDemo.Api.Services;

public interface IContractRegistryAuthorizer
{
    bool IsAuthorized(
        ClaimsPrincipal user,
        string policy,
        string? operatorId,
        bool mutation);
}

public sealed class ContractRegistryAuthorizer(
    IConfiguration configuration,
    ContractRegistryObservability? observability = null) :
    IContractRegistryAuthorizer
{
    public bool IsAuthorized(
        ClaimsPrincipal user,
        string policy,
        string? operatorId,
        bool mutation)
    {
        if (mutation && ContractRegistrySourceModeParser.Parse(
                configuration["ContractRegistry:SourceMode"]) !=
            ContractRegistrySourceMode.Postgres)
        {
            if (policy == "ContractAdmin.Publish")
            {
                observability?.RecordUnauthorizedPublish();
            }
            return false;
        }
        if (policy == "ContractAdmin.Publish" &&
            !configuration.GetValue(
                "ContractRegistry:PublishingEnabled",
                false))
        {
            observability?.RecordUnauthorizedPublish();
            return false;
        }

        if (user.Identity?.IsAuthenticated == true &&
            (user.IsInRole(policy) || user.HasClaim("permission", policy)))
        {
            return !mutation || !string.IsNullOrWhiteSpace(operatorId);
        }

        var authorized = configuration.GetValue(
                "ContractRegistry:AllowLocalOperatorHeader",
                false) &&
            (!mutation || !string.IsNullOrWhiteSpace(operatorId));
        if (!authorized && policy == "ContractAdmin.Publish")
        {
            observability?.RecordUnauthorizedPublish();
        }
        return authorized;
    }
}
