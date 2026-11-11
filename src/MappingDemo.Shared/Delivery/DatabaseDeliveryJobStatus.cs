namespace MappingDemo.Shared.Delivery;

public static class DatabaseDeliveryJobStatus
{
    public const string Pending = "Pending";
    public const string Delivering = "Delivering";
    public const string Delivered = "Delivered";
    public const string AlreadyDelivered = "AlreadyDelivered";
    public const string RetryableFailed = "RetryableFailed";
    public const string PermanentFailed = "PermanentFailed";
    public const string DeliveryConflict = "DeliveryConflict";
    public const string BlockedSchemaDrift = "BlockedSchemaDrift";
    public const string UnknownOutcome = "UnknownOutcome";
}
