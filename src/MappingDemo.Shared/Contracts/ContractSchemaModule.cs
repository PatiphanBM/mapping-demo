using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MappingDemo.Shared.Transformation;
using System.Collections.Concurrent;

namespace MappingDemo.Shared.Contracts;

public sealed class ContractSchemaModule
{
    private static readonly ConcurrentDictionary<
        string,
        (TargetSchema Schema, IReadOnlyList<ContractPathDescriptor> Paths)> Cache =
        new(StringComparer.Ordinal);

    public ContractSchemaCompilationResult Compile(string schemaJson)
    {
        if (Encoding.UTF8.GetByteCount(schemaJson) > ContractRegistryModule.MaxSchemaBytes)
        {
            return ContractSchemaCompilationResult.Failure(
                "SchemaTooLarge",
                "Contract schema exceeds the configured size limit.");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(schemaJson, new JsonDocumentOptions
            {
                MaxDepth = 64,
                CommentHandling = JsonCommentHandling.Disallow,
                AllowTrailingCommas = false
            });
        }
        catch (JsonException exception)
        {
            return ContractSchemaCompilationResult.Failure(
                "InvalidJson",
                $"Contract schema is not valid JSON: {exception.Message}");
        }

        using (document)
        {
            var diagnostics = new List<ContractDiagnostic>();
            var context = new CompilationContext(document.RootElement, diagnostics);
            var root = context.CompileNode(
                document.RootElement,
                "$",
                [],
                isRequired: true,
                new HashSet<string>(StringComparer.Ordinal));
            if (root is null || diagnostics.Count > 0)
            {
                return new ContractSchemaCompilationResult(
                    null,
                    null,
                    null,
                    [],
                    diagnostics);
            }

            var canonical = Canonicalize(document.RootElement);
            var hash = "sha256:" + Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
                .ToLowerInvariant();
            if (Cache.TryGetValue(hash, out var cached))
            {
                return new ContractSchemaCompilationResult(
                    canonical,
                    hash,
                    cached.Schema,
                    cached.Paths,
                    []);
            }
            var schema = new TargetSchema(root);
            var paths = BuildCatalog(root);
            Cache.TryAdd(hash, (schema, paths));
            return new ContractSchemaCompilationResult(
                canonical,
                hash,
                schema,
                paths,
                []);
        }
    }

    private static IReadOnlyList<ContractPathDescriptor> BuildCatalog(TargetNode root)
    {
        var result = new List<ContractPathDescriptor>();
        Visit(root, result);
        return result;
    }

    private static void Visit(TargetNode node, ICollection<ContractPathDescriptor> result)
    {
        if (node.PathTokens.Count > 0)
        {
            result.Add(new ContractPathDescriptor(
                string.Join('.', node.PathTokens),
                node.Kind,
                node.Kind == TargetNodeKind.Scalar,
                node.IsRequired,
                node.IsNullable,
                ToContractType(node.ScalarType),
                node.Constraints,
                node.Description));
        }

        foreach (var child in node.Properties)
        {
            Visit(child, result);
        }

        if (node.ItemSchema is not null)
        {
            Visit(node.ItemSchema, result);
        }
    }

    private static ContractScalarType? ToContractType(TargetScalarType? type) =>
        type switch
        {
            TargetScalarType.Text => ContractScalarType.Text,
            TargetScalarType.Date => ContractScalarType.Date,
            TargetScalarType.Time => ContractScalarType.Time,
            TargetScalarType.DateTime => ContractScalarType.DateTime,
            TargetScalarType.Integer => ContractScalarType.Integer,
            TargetScalarType.Decimal => ContractScalarType.Number,
            TargetScalarType.Boolean => ContractScalarType.Boolean,
            TargetScalarType.Guid => ContractScalarType.Guid,
            null => null,
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

    private static string Canonicalize(JsonElement element)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions
        {
            Indented = false
        });
        WriteCanonical(writer, element);
        writer.Flush();
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject()
                    .OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonical(writer, item);
                }
                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }

    private sealed class CompilationContext(
        JsonElement document,
        ICollection<ContractDiagnostic> diagnostics)
    {
        public TargetNode? CompileNode(
            JsonElement element,
            string name,
            IReadOnlyList<string> path,
            bool isRequired,
            ISet<string> referenceStack)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                Error(path, "SchemaNodeMustBeObject", "Every schema node must be a JSON object.");
                return null;
            }

            if (element.TryGetProperty("oneOf", out _) ||
                element.TryGetProperty("anyOf", out _) ||
                element.TryGetProperty("allOf", out _))
            {
                Error(path, "AmbiguousUnionNotSupported", "oneOf, anyOf and allOf are not supported.");
                return null;
            }

            if (element.TryGetProperty("$ref", out var referenceElement))
            {
                var reference = referenceElement.GetString() ?? string.Empty;
                if (!reference.StartsWith("#/", StringComparison.Ordinal))
                {
                    Error(path, "ExternalReferenceNotSupported", "Only local JSON Pointer references are supported.");
                    return null;
                }

                if (!referenceStack.Add(reference))
                {
                    Error(path, "RecursiveReferenceNotSupported", "Recursive schema references are not supported.");
                    return null;
                }

                var resolved = ResolvePointer(reference);
                if (resolved is null)
                {
                    Error(path, "LocalReferenceNotFound", $"Local reference '{reference}' was not found.");
                    return null;
                }

                var node = CompileNode(
                    resolved.Value,
                    name,
                    path,
                    isRequired,
                    referenceStack);
                referenceStack.Remove(reference);
                return node;
            }

            var (type, nullable) = ReadType(element, path);
            if (type is null)
            {
                return null;
            }

            var description = element.TryGetProperty("description", out var descriptionElement)
                ? descriptionElement.GetString()
                : null;
            return type switch
            {
                "object" => CompileObject(element, name, path, isRequired, nullable, description, referenceStack),
                "array" => CompileArray(element, name, path, isRequired, nullable, description, referenceStack),
                "string" or "integer" or "number" or "boolean" => new TargetNode(
                    name,
                    TargetNodeKind.Scalar,
                    isRequired,
                    path,
                    ScalarType(type, element, path),
                    Constraints(element),
                    isNullable: nullable,
                    description: description),
                _ => UnsupportedType(path, type)
            };
        }

        private TargetNode? CompileObject(
            JsonElement element,
            string name,
            IReadOnlyList<string> path,
            bool required,
            bool nullable,
            string? description,
            ISet<string> stack)
        {
            if (element.TryGetProperty("additionalProperties", out var additional) &&
                additional.ValueKind == JsonValueKind.True)
            {
                Error(path, "UnconstrainedAdditionalPropertiesNotSupported", "additionalProperties: true is not supported.");
                return null;
            }

            var requiredNames = element.TryGetProperty("required", out var requiredElement)
                ? requiredElement.EnumerateArray().Select(item => item.GetString()!).ToHashSet(StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);
            var children = new List<TargetNode>();
            if (element.TryGetProperty("properties", out var properties))
            {
                foreach (var property in properties.EnumerateObject())
                {
                    var childPath = path.Append(property.Name).ToArray();
                    var child = CompileNode(
                        property.Value,
                        property.Name,
                        childPath,
                        requiredNames.Contains(property.Name),
                        new HashSet<string>(stack, StringComparer.Ordinal));
                    if (child is not null)
                    {
                        children.Add(child);
                    }
                }
            }

            return new TargetNode(
                name,
                TargetNodeKind.Object,
                required,
                path,
                properties: children,
                isNullable: nullable,
                description: description);
        }

        private TargetNode? CompileArray(
            JsonElement element,
            string name,
            IReadOnlyList<string> path,
            bool required,
            bool nullable,
            string? description,
            ISet<string> stack)
        {
            if (!element.TryGetProperty("items", out var items))
            {
                Error(path, "ArrayItemsRequired", "Array schemas must define items.");
                return null;
            }

            var itemPath = path.Count == 0
                ? ["[]"]
                : path.Take(path.Count - 1).Append(path[^1] + "[]").ToArray();
            var item = CompileNode(items, "[]", itemPath, false, stack);
            return item is null
                ? null
                : new TargetNode(
                    name,
                    TargetNodeKind.Array,
                    required,
                    path,
                    itemSchema: item,
                    isNullable: nullable,
                    description: description);
        }

        private (string? Type, bool Nullable) ReadType(
            JsonElement element,
            IReadOnlyList<string> path)
        {
            if (!element.TryGetProperty("type", out var typeElement))
            {
                Error(path, "SchemaTypeRequired", "Schema type is required.");
                return (null, false);
            }

            if (typeElement.ValueKind == JsonValueKind.String)
            {
                return (typeElement.GetString(), false);
            }

            if (typeElement.ValueKind == JsonValueKind.Array)
            {
                var types = typeElement.EnumerateArray()
                    .Select(item => item.GetString())
                    .ToArray();
                var concrete = types.Where(item => item != "null").ToArray();
                if (types.Length == 2 && concrete.Length == 1 && types.Contains("null"))
                {
                    return (concrete[0], true);
                }
            }

            Error(path, "AmbiguousUnionNotSupported", "Only a single type or a nullable [type, null] union is supported.");
            return (null, false);
        }

        private TargetScalarType ScalarType(
            string type,
            JsonElement element,
            IReadOnlyList<string> path)
        {
            if (type == "integer") return TargetScalarType.Integer;
            if (type == "number") return TargetScalarType.Decimal;
            if (type == "boolean") return TargetScalarType.Boolean;
            var format = element.TryGetProperty("format", out var formatElement)
                ? formatElement.GetString()
                : null;
            return format switch
            {
                null => TargetScalarType.Text,
                "date" => TargetScalarType.Date,
                "time" => TargetScalarType.Time,
                "date-time" => TargetScalarType.DateTime,
                "uuid" => TargetScalarType.Guid,
                _ => UnsupportedFormat(path, format)
            };
        }

        private TargetScalarType UnsupportedFormat(IReadOnlyList<string> path, string format)
        {
            Error(path, "UnsupportedStringFormat", $"String format '{format}' is not supported.");
            return TargetScalarType.Text;
        }

        private static TargetConstraints Constraints(JsonElement element) =>
            new(
                Length: element.TryGetProperty("maxLength", out var maxLength) ? maxLength.GetInt32() : null,
                Precision: null,
                Scale: null,
                Minimum: element.TryGetProperty("minimum", out var minimum) ? minimum.GetDecimal() : null,
                Maximum: element.TryGetProperty("maximum", out var maximum) ? maximum.GetDecimal() : null,
                Pattern: element.TryGetProperty("pattern", out var pattern) ? pattern.GetString() : null);

        private JsonElement? ResolvePointer(string reference)
        {
            var current = document;
            foreach (var rawToken in reference[2..].Split('/'))
            {
                var token = rawToken.Replace("~1", "/", StringComparison.Ordinal)
                    .Replace("~0", "~", StringComparison.Ordinal);
                if (current.ValueKind != JsonValueKind.Object ||
                    !current.TryGetProperty(token, out current))
                {
                    return null;
                }
            }
            return current;
        }

        private TargetNode? UnsupportedType(IReadOnlyList<string> path, string type)
        {
            Error(path, "UnsupportedSchemaType", $"Schema type '{type}' is not supported.");
            return null;
        }

        private void Error(IReadOnlyList<string> path, string code, string message) =>
            diagnostics.Add(new ContractDiagnostic(
                path.Count == 0 ? "$" : string.Join('.', path),
                code,
                message));
    }
}

public sealed record ContractSchemaCompilationResult(
    string? CanonicalSchema,
    string? SchemaHash,
    TargetSchema? TargetSchema,
    IReadOnlyList<ContractPathDescriptor> PathCatalog,
    IReadOnlyList<ContractDiagnostic> Diagnostics)
{
    public bool IsSuccess => TargetSchema is not null;

    public static ContractSchemaCompilationResult Failure(string code, string message) =>
        new(null, null, null, [], [new ContractDiagnostic("$", code, message)]);
}

public sealed record ContractPathDescriptor(
    string Path,
    TargetNodeKind Kind,
    bool IsAssignable,
    bool IsRequired,
    bool IsNullable,
    ContractScalarType? ScalarType,
    TargetConstraints? Constraints,
    string? Description);

public enum ContractScalarType
{
    Text,
    Integer,
    Number,
    Boolean,
    Date,
    Time,
    DateTime,
    Guid
}

public sealed record ContractDiagnostic(string Path, string Code, string Message);
