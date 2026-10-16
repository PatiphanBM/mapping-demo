namespace MappingDemo.Shared.Transformation;

public static class TransformationCapabilities
{
    public const int LanguageVersion = 1;

    public const string SqlServerLegacyProfile = "sqlserver-legacy-v1";

    public const string AsiaBangkokTimeZone = "Asia/Bangkok";

    public const string FormatRegNoV1 = "formatRegNo@1";

    public const string MtiOriginalPolicyExistsV1 =
        "mtiOriginalPolicyExists@1";

    public const int MaxOutputs = 32;

    public const int MaxFieldsPerOutput = 256;

    public const int MaxCollectionItems = 256;

    public const int MaxExpressionDepth = 32;

    public const int MaxExpressionNodes = 2048;

    public static IReadOnlyList<string> SemanticsProfiles { get; } =
        Array.AsReadOnly([SqlServerLegacyProfile]);

    public static IReadOnlyList<ExpressionValueType> TargetConversionTypes
    { get; } = Array.AsReadOnly(
        [
            ExpressionValueType.Decimal,
            ExpressionValueType.Integer,
            ExpressionValueType.Date,
            ExpressionValueType.DateTime,
            ExpressionValueType.Boolean,
            ExpressionValueType.Guid
        ]);

    public static IReadOnlyList<string> DateInputFormats { get; } =
        Array.AsReadOnly(["yyyy-MM-dd", "dd/MM/yyyy", "yyyyMMdd"]);

    public static IReadOnlyList<string> DateTimeInputFormats { get; } =
        Array.AsReadOnly(
        [
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-ddTHH:mm:ssK",
            "yyyy-MM-dd HH:mm:ss",
            "dd/MM/yyyy HH:mm:ss",
            "yyyyMMddHHmmss"
        ]);

    public static IReadOnlyList<string> BooleanOperators { get; } =
        Array.AsReadOnly(
        [
            "eq",
            "ne",
            "gt",
            "gte",
            "lt",
            "lte",
            "isNull",
            "isBlank",
            "notBlank",
            "like",
            "in",
            "notIn",
            "and",
            "or",
            "not",
            "exists",
            "notExists"
        ]);

    public static IReadOnlyList<string> ValueExpressions { get; } =
        Array.AsReadOnly(
        [
            "source",
            "metadata",
            "literal",
            "coalesce",
            "defaultIfBlank",
            "trim",
            "case",
            "substring",
            "convert",
            "currentDate",
            "call"
        ]);

    public static IReadOnlyList<string> Functions { get; } =
        Array.AsReadOnly([FormatRegNoV1]);

    public static IReadOnlyList<string> Lookups { get; } =
        Array.AsReadOnly([MtiOriginalPolicyExistsV1]);

    public static IReadOnlyList<string> MetadataFields { get; } =
        Array.AsReadOnly(
        ["fileName", "rowNumber", "physicalLineNumber", "evaluationTime"]);
}
