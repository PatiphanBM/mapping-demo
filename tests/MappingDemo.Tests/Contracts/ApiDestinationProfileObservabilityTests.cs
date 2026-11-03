using System.Text.Json;
using MappingDemo.Shared.Contracts;

namespace MappingDemo.Tests.Contracts;

public sealed class ApiDestinationProfileObservabilityTests
{
    [Fact]
    public void Snapshot_exposes_safe_metrics_and_required_alerts()
    {
        var observability = new ApiDestinationProfileObservability();
        observability.RecordValidation("Sandbox", false);
        observability.RecordValidation("Sandbox", false);
        observability.RecordValidation("Sandbox", false);
        observability.RecordMutation(ApiDestinationProfileMutationKind.Activate);
        observability.RecordMutation(ApiDestinationProfileMutationKind.Disable);
        observability.RecordCacheAccess(
            ApiDestinationProfileCacheKind.Pinned,
            hit: true);
        observability.RecordCacheAccess(
            ApiDestinationProfileCacheKind.Pinned,
            hit: false);
        observability.RecordResolutionLatency(
            ApiDestinationProfileCatalogOperation.ResolvePinned,
            TimeSpan.FromMilliseconds(12));
        observability.RecordResolutionFailure(
            "UnknownPinnedProfileVersion");
        observability.RecordResolutionFailure("ProfileCatalogUnavailable");
        observability.RecordResolutionFailure(
            "ProfileCatalogParityMismatch");
        observability.RecordUnauthorizedActivation();

        var snapshot = observability.Snapshot();
        var alertCodes = snapshot.Alerts.Select(alert => alert.Code).ToArray();

        Assert.Contains("PinnedVersionMissing", alertCodes);
        Assert.Contains("ProfileCatalogUnavailable", alertCodes);
        Assert.Contains("ProfileCatalogSourceMismatch", alertCodes);
        Assert.Contains("UnauthorizedActivationAttempt", alertCodes);
        Assert.Contains("RepeatedValidationFailure", alertCodes);
        Assert.Equal(3, snapshot.ConsecutiveValidationFailures);
        Assert.Equal(12, Assert.Single(snapshot.ResolveLatency)
            .MaximumMilliseconds);

        var json = JsonSerializer.Serialize(snapshot);
        Assert.DoesNotContain("partner-a", json, StringComparison.Ordinal);
        Assert.DoesNotContain("example.test", json, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-reference", json, StringComparison.Ordinal);
        Assert.DoesNotContain("contract/1", json, StringComparison.Ordinal);
        Assert.DoesNotContain("operator-a", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Successful_validation_clears_repeated_failure_alert()
    {
        var observability = new ApiDestinationProfileObservability();
        for (var attempt = 0; attempt < 3; attempt++)
        {
            observability.RecordValidation("Production", false);
        }
        observability.RecordValidation("Production", true);

        var snapshot = observability.Snapshot();

        Assert.Equal(0, snapshot.ConsecutiveValidationFailures);
        Assert.DoesNotContain(
            snapshot.Alerts,
            alert => alert.Code == "RepeatedValidationFailure");
    }
}
