using MappingDemo.Shared.Delivery;

namespace MappingDemo.Tests.Delivery;

public sealed class DeliveryStatusCalculatorTests
{
    [Theory]
    [InlineData(0, 0, 0, 0, 0, null)]
    [InlineData(1, 0, 0, 0, 0, DeliveryJobStatus.Pending)]
    [InlineData(0, 1, 0, 0, 0, DeliveryJobStatus.Delivering)]
    [InlineData(0, 0, 2, 0, 0, DeliveryJobStatus.Delivered)]
    [InlineData(0, 0, 1, 1, 0, DeliveryJobStatus.RetryableFailed)]
    [InlineData(0, 0, 1, 0, 1, DeliveryJobStatus.PermanentFailed)]
    public void Calculate_is_deterministic_for_mixed_outputs(
        int pending,
        int delivering,
        int delivered,
        int retryable,
        int permanent,
        string? expected)
    {
        Assert.Equal(
            expected,
            DeliveryStatusCalculator.Calculate(
                pending,
                delivering,
                delivered,
                retryable,
                permanent));
    }
}
