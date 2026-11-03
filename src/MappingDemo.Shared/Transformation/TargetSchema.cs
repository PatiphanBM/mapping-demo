namespace MappingDemo.Shared.Transformation;

public sealed class TargetSchema
{
    public TargetSchema(TargetNode root)
    {
        ArgumentNullException.ThrowIfNull(root);

        Root = root;
    }

    public TargetNode Root { get; }
}

public sealed class TargetNode
{
    public TargetNode(
        string name,
        TargetNodeKind kind,
        bool isRequired,
        IEnumerable<string> pathTokens,
        TargetScalarType? scalarType = null,
        TargetConstraints? constraints = null,
        IEnumerable<TargetNode>? properties = null,
        TargetNode? itemSchema = null,
        bool isNullable = false,
        string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(pathTokens);

        Name = name;
        Kind = kind;
        IsRequired = isRequired;
        PathTokens = Array.AsReadOnly(pathTokens.ToArray());
        ScalarType = scalarType;
        Constraints = constraints;
        Properties = Array.AsReadOnly(properties?.ToArray() ?? []);
        ItemSchema = itemSchema;
        IsNullable = isNullable;
        Description = description;
    }

    public string Name { get; }

    public TargetNodeKind Kind { get; }

    public bool IsRequired { get; }

    public IReadOnlyList<string> PathTokens { get; }

    public TargetScalarType? ScalarType { get; }

    public TargetConstraints? Constraints { get; }

    public IReadOnlyList<TargetNode> Properties { get; }

    public TargetNode? ItemSchema { get; }

    public bool IsNullable { get; }

    public string? Description { get; }
}

public enum TargetNodeKind
{
    Scalar,
    Object,
    Array
}

public enum TargetScalarType
{
    Text,
    Date,
    Time,
    Decimal,
    Boolean,
    Integer,
    DateTime,
    Guid
}

public sealed record TargetConstraints(
    int? Length = null,
    int? Precision = null,
    int? Scale = null,
    decimal? Minimum = null,
    decimal? Maximum = null,
    string? Pattern = null);
