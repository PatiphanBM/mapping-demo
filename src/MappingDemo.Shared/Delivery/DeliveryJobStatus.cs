namespace MappingDemo.Shared.Delivery;

public static class DeliveryJobStatus
{
    public const string Pending = "Pending";
    public const string Delivering = "Delivering";
    public const string Delivered = "Delivered";
    public const string RetryableFailed = "RetryableFailed";
    public const string PermanentFailed = "PermanentFailed";
}
