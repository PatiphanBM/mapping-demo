using MappingDemo.Shared.Delivery;

namespace MappingDemo.Tests.Delivery;

public sealed class DeliveryRetryPolicyTests
{
    [Theory]
    [InlineData(200, DeliveryFailureKind.Delivered)]
    [InlineData(202, DeliveryFailureKind.Delivered)]
    [InlineData(400, DeliveryFailureKind.Permanent)]
    [InlineData(409, DeliveryFailureKind.Permanent)]
    [InlineData(408, DeliveryFailureKind.Retryable)]
    [InlineData(429, DeliveryFailureKind.Retryable)]
    [InlineData(500, DeliveryFailureKind.Retryable)]
    [InlineData(503, DeliveryFailureKind.Retryable)]
    public void Classify_matches_approved_policy(
        int statusCode,
        DeliveryFailureKind expected)
    {
        Assert.Equal(
            expected,
            DeliveryRetryPolicy.Classify(statusCode, false));
    }

    [Fact]
    public void Network_and_timeout_failures_are_retryable()
    {
        Assert.Equal(
            DeliveryFailureKind.Retryable,
            DeliveryRetryPolicy.Classify(null, true));
    }

    [Fact]
    public void Delay_is_bounded_and_honors_retry_after()
    {
        Assert.Equal(
            TimeSpan.FromSeconds(5),
            DeliveryRetryPolicy.Delay(1, jitterFactor: 1));
        Assert.Equal(
            TimeSpan.FromMinutes(30),
            DeliveryRetryPolicy.Delay(
                6,
                TimeSpan.FromHours(2),
                jitterFactor: 1));
        Assert.Equal(
            TimeSpan.FromMinutes(30),
            DeliveryRetryPolicy.Delay(6, jitterFactor: 1.5));
    }
}
