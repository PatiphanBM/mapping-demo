using System.Text.Json;

namespace MappingDemo.Tests.Delivery;

public sealed class DeliveryStubFixtureTests
{
    [Fact]
    public void Fixture_covers_every_required_partner_outcome()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "Phase24",
            "delivery-stub-scenarios.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var names = document.RootElement.GetProperty("scenarios")
            .EnumerateArray()
            .Select(item => item.GetProperty("name").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(
            new HashSet<string>(StringComparer.Ordinal)
            {
                "success",
                "bad-request",
                "request-timeout",
                "rate-limited",
                "server-error",
                "timeout",
                "commit-before-timeout",
                "idempotency-conflict"
            },
            names);
    }
}
