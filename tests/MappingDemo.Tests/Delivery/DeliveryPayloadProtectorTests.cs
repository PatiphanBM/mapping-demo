using System.Text;
using MappingDemo.Worker.Delivery;
using Microsoft.Extensions.Configuration;

namespace MappingDemo.Tests.Delivery;

public sealed class DeliveryPayloadProtectorTests
{
    [Fact]
    public async Task Protect_round_trips_without_storing_plaintext()
    {
        var key = Convert.ToBase64String(Enumerable.Range(0, 32)
            .Select(value => (byte)value)
            .ToArray());
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApiDelivery:PayloadEncryptionKeyReference"] = "test-key"
            })
            .Build();
        var protector = new DeliveryPayloadProtector(
            new TestSecretProvider(key),
            configuration);
        var plaintext = Encoding.UTF8.GetBytes("{\"policy\":\"secret\"}");

        var protectedPayload = await protector.ProtectAsync(
            plaintext,
            CancellationToken.None);
        var roundTrip = await protector.UnprotectAsync(
            protectedPayload.Ciphertext,
            protectedPayload.Nonce,
            protectedPayload.Tag,
            protectedPayload.KeyReference,
            CancellationToken.None);

        Assert.Equal(plaintext, roundTrip);
        Assert.DoesNotContain(
            "secret",
            Encoding.UTF8.GetString(protectedPayload.Ciphertext));
    }

    private sealed class TestSecretProvider(string value)
        : IDeliverySecretProvider
    {
        public ValueTask<string> GetSecretAsync(
            string reference,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(value);
    }
}
