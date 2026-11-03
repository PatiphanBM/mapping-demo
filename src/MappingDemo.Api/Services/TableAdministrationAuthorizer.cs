using System.Security.Claims;
using MappingDemo.Shared.Tables;

namespace MappingDemo.Api.Services;

public interface ITableAdministrationAuthorizer
{
    bool IsAuthorized(
        ClaimsPrincipal user,
        string policy,
        string? operatorId,
        bool mutation);
}

public sealed class TableAdministrationAuthorizer(
    IConfiguration configuration,
    TableAdministrationObservability? observability = null) :
    ITableAdministrationAuthorizer
{
    public bool IsAuthorized(
        ClaimsPrincipal user,
        string policy,
        string? operatorId,
        bool mutation)
    {
        if (mutation && !configuration.GetValue(
                "TableAdministration:MutationsEnabled",
                false))
        {
            observability?.RecordUnauthorizedMutation();
            return false;
        }

        if (user.Identity?.IsAuthenticated == true &&
            (user.IsInRole(policy) || user.HasClaim("permission", policy)))
        {
            return !mutation || !string.IsNullOrWhiteSpace(operatorId);
        }

        var authorized = configuration.GetValue(
                "TableAdministration:AllowLocalOperatorHeader",
                false) &&
            (!mutation || !string.IsNullOrWhiteSpace(operatorId));
        if (!authorized && mutation)
        {
            observability?.RecordUnauthorizedMutation();
        }
        return authorized;
    }
}
