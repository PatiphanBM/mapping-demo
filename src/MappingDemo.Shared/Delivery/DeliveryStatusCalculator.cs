namespace MappingDemo.Shared.Delivery;

public static class DeliveryStatusCalculator
{
    public static string? Calculate(
        int pending,
        int delivering,
        int delivered,
        int retryableFailed,
        int permanentFailed)
    {
        var total = pending + delivering + delivered + retryableFailed +
            permanentFailed;
        if (total == 0)
        {
            return null;
        }

        if (permanentFailed > 0)
        {
            return DeliveryJobStatus.PermanentFailed;
        }

        if (retryableFailed > 0)
        {
            return DeliveryJobStatus.RetryableFailed;
        }

        if (delivering > 0)
        {
            return DeliveryJobStatus.Delivering;
        }

        if (pending > 0)
        {
            return DeliveryJobStatus.Pending;
        }

        return DeliveryJobStatus.Delivered;
    }
}
