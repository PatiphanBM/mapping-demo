using System.Net;
using MappingDemo.Shared.Contracts;

namespace MappingDemo.Tests.Contracts;

public sealed class ApiDestinationProfileNetworkPolicyTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.10.0.1")]
    [InlineData("169.254.1.1")]
    [InlineData("172.16.0.1")]
    [InlineData("192.168.1.1")]
    [InlineData("100.64.0.1")]
    [InlineData("198.51.100.1")]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    [InlineData("fc00::1")]
    [InlineData("2001:db8::1")]
    public void Forbidden_address_ranges_are_rejected(string value)
    {
        Assert.True(ApiDestinationProfileNetworkPolicy.IsForbiddenAddress(
            IPAddress.Parse(value)));
    }

    [Fact]
    public void Public_address_is_allowed()
    {
        Assert.False(ApiDestinationProfileNetworkPolicy.IsForbiddenAddress(
            IPAddress.Parse("8.8.8.8")));
    }

    [Fact]
    public async Task Mixed_public_and_private_dns_answer_is_rejected()
    {
        static ValueTask<IPAddress[]> Resolve(
            string _,
            CancellationToken __) =>
            ValueTask.FromResult(new[]
            {
                IPAddress.Parse("8.8.8.8"),
                IPAddress.Parse("10.0.0.1")
            });

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await ApiDestinationProfileNetworkPolicy.ResolveAndValidateAsync(
                "delivery.example.test",
                CancellationToken.None,
                Resolve));
    }

    [Fact]
    public void Secure_handler_does_not_follow_redirects()
    {
        using var handler =
            ApiDestinationProfileNetworkPolicy.CreateSocketsHttpHandler();

        Assert.False(handler.AllowAutoRedirect);
        Assert.NotNull(handler.ConnectCallback);
    }
}
