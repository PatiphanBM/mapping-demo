using System.Text.Json;
using MappingDemo.Shared.Contracts;

namespace MappingDemo.Tests.Contracts;

public sealed class ContractRegistryObservabilityTests
{
    [Fact]
    public void Snapshot_exposes_safe_metrics_and_required_alerts()
    {
        var observability = new ContractRegistryObservability();
        observability.RecordValidation(false);
        observability.RecordValidation(false);
        observability.RecordValidation(false);
        observability.RecordMutation(
            new PublishContractVersionCommand(
                1,
                1,
                false,
                "operator-a",
                "Publish proposal contract",
                "publish-proposal"),
            success: false);
        observability.RecordCacheAccess("exact", hit: false);
        observability.RecordResolutionFailure(
            "ContractRegistryUnavailable");
        observability.RecordResolutionFailure("ContractRegistryCorrupt");
        observability.RecordUnauthorizedPublish();

        var snapshot = observability.Snapshot();
        var alerts = snapshot.Alerts.Select(item => item.Code).ToArray();

        Assert.Contains("ContractRegistryIntegrityOrAvailability", alerts);
        Assert.Contains("RepeatedContractValidationFailure", alerts);
        Assert.Contains("UnauthorizedContractPublishAttempt", alerts);
        Assert.Equal(3, snapshot.ConsecutiveValidationFailures);
        var json = JsonSerializer.Serialize(snapshot);
        Assert.DoesNotContain("proposal", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("operator-a", json, StringComparison.Ordinal);
        Assert.DoesNotContain("publish-proposal", json, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Successful_validation_clears_repeated_failure_alert()
    {
        var observability = new ContractRegistryObservability();
        observability.RecordValidation(false);
        observability.RecordValidation(false);
        observability.RecordValidation(false);
        observability.RecordValidation(true);

        var snapshot = observability.Snapshot();

        Assert.Equal(0, snapshot.ConsecutiveValidationFailures);
        Assert.DoesNotContain(snapshot.Alerts, item =>
            item.Code == "RepeatedContractValidationFailure");
    }
}
