using System.Security.Claims;
using MappingDemo.Shared.Contracts;

namespace MappingDemo.Api.Services;

public interface IProfileAdministrationAuthorizer
{
    bool IsAuthorized(
        ClaimsPrincipal user,
        string policy,
        string? operatorId,
        bool mutation);
}

public sealed class ProfileAdministrationAuthorizer(
    IConfiguration configuration,
    ApiDestinationProfileObservability? observability = null) :
    IProfileAdministrationAuthorizer
{
    public bool IsAuthorized(
        ClaimsPrincipal user,
        string policy,
        string? operatorId,
        bool mutation)
    {
        bool authorized;
        if (mutation && ApiDestinationProfileCatalogSourceModeParser.Parse(
                configuration[
                    "ApiDestinationProfileCatalog:SourceMode"]) ==
            ApiDestinationProfileCatalogSourceMode.Compare)
        {
            authorized = false;
        }
        else if (user.Identity?.IsAuthenticated == true &&
            (user.IsInRole(policy) || user.HasClaim("permission", policy)))
        {
            authorized = !mutation || !string.IsNullOrWhiteSpace(operatorId);
        }
        else
        {
            authorized = configuration.GetValue(
                "ProfileAdministration:AllowLocalOperatorHeader", false) &&
                (!mutation || !string.IsNullOrWhiteSpace(operatorId));
        }

        if (!authorized && mutation &&
            policy.StartsWith("ProfileAdmin.Activate", StringComparison.Ordinal))
        {
            observability?.RecordUnauthorizedActivation();
        }
        return authorized;
    }
}
