using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MappingDemo.Shared.Transformation;
using System.Text.RegularExpressions;

namespace MappingDemo.Shared.Contracts;

public sealed class PayloadBindingModule
{
    public PayloadBindingResult Bind(
        TargetSchema schema,
        IReadOnlyDictionary<string, object?> mappedDocument,
        IReadOnlyList<PayloadBinding> bindings,
        PayloadBindingContext context)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(mappedDocument);
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(context);

        var diagnostics = new List<ContractDiagnostic>();
        var assembler = new DocumentAssembler();
        Flatten(mappedDocument, [], assembler);
        var boundPaths = new HashSet<string>(StringComparer.Ordinal);

        foreach (var binding in bindings)
        {
            var resolved = TargetPath.Resolve(schema, binding.TargetPath);
            if (resolved is null)
            {
                diagnostics.Add(new(
                    binding.TargetPath,
                    "UnknownBindingPath",
                    "Payload binding target path does not exist."));
                continue;
            }
            if (resolved.Node.Kind != TargetNodeKind.Scalar)
            {
                diagnostics.Add(new(
                    binding.TargetPath,
                    "BindingTargetNotScalar",
                    "Payload binding target must be a scalar leaf."));
                continue;
            }
            if (!boundPaths.Add(binding.TargetPath))
            {
                diagnostics.Add(new(
                    binding.TargetPath,
                    "DuplicatePayloadBinding",
                    "Payload binding target is duplicated."));
                continue;
            }
            if (TryGet(mappedDocument, resolved.Tokens, out _))
            {
                diagnostics.Add(new(
                    binding.TargetPath,
                    "SystemMappedPathConflict",
                    "System-bound path cannot also be written by business mapping."));
                continue;
            }

            var raw = RuntimeValue(binding.Source, context);
            if (!TryConvert(raw, resolved.Node, out var value))
            {
                diagnostics.Add(new(
                    binding.TargetPath,
                    "BindingTypeMismatch",
                    $"Runtime source '{binding.Source}' is incompatible with the target type."));
                continue;
            }
            assembler.Set(resolved.Tokens, value);
        }

        if (diagnostics.Count > 0)
        {
            return new PayloadBindingResult(null, diagnostics);
        }

        MaterializeRequiredContainers(schema.Root, [], assembler);
        var document = assembler.Build();
        ValidateNode(schema.Root, document, "$", diagnostics);
        if (diagnostics.Count > 0)
        {
            return new PayloadBindingResult(null, diagnostics);
        }

        var json = JsonSerializer.Serialize(document, new JsonSerializerOptions(
            JsonSerializerDefaults.Web));
        var hash = "sha256:" + Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
        return new PayloadBindingResult(
            new DeliveryPayload(document, json, hash),
            []);
    }

    private static void Flatten(
        object? value,
        IReadOnlyList<TargetPathToken> path,
        DocumentAssembler assembler)
    {
        if (value is IReadOnlyDictionary<string, object?> dictionary)
        {
            foreach (var pair in dictionary.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                Flatten(pair.Value, path.Append(new PropertyPathToken(pair.Key)).ToArray(), assembler);
            }
            return;
        }
        if (value is not string && value is IEnumerable<object?> sequence)
        {
            var index = 0;
            foreach (var item in sequence)
            {
                Flatten(item, path.Append(new IndexPathToken(index++)).ToArray(), assembler);
            }
            if (index == 0 && path.Count > 0)
            {
                assembler.Set(path, new List<object?>());
            }
            return;
        }
        if (path.Count > 0)
        {
            assembler.Set(path, value);
        }
    }

    private static bool TryGet(
        object? current,
        IReadOnlyList<TargetPathToken> tokens,
        out object? value)
    {
        foreach (var token in tokens)
        {
            switch (token)
            {
                case PropertyPathToken property
                    when current is IReadOnlyDictionary<string, object?> dictionary &&
                         dictionary.TryGetValue(property.Name, out var child):
                    current = child;
                    break;
                case IndexPathToken index
                    when current is IReadOnlyList<object?> list && index.Index < list.Count:
                    current = list[index.Index];
                    break;
                default:
                    value = null;
                    return false;
            }
        }
        value = current;
        return true;
    }

    private static void MaterializeRequiredContainers(
        TargetNode node,
        IReadOnlyList<TargetPathToken> path,
        DocumentAssembler assembler)
    {
        if (node.Kind != TargetNodeKind.Object)
        {
            return;
        }

        foreach (var child in node.Properties)
        {
            var childPath = path
                .Append<TargetPathToken>(new PropertyPathToken(child.Name))
                .ToArray();
            var exists = TryGet(assembler.Build(), childPath, out var value);
            if (!exists && child.IsRequired &&
                child.Kind is TargetNodeKind.Object or TargetNodeKind.Array)
            {
                value = child.Kind == TargetNodeKind.Object
                    ? new SortedDictionary<string, object?>(StringComparer.Ordinal)
                    : new List<object?>();
                assembler.Set(childPath, value);
                exists = true;
            }

            if (exists && value is not null && child.Kind == TargetNodeKind.Object)
            {
                MaterializeRequiredContainers(child, childPath, assembler);
            }
        }
    }

    private static object RuntimeValue(
        RuntimeValueSource source,
        PayloadBindingContext context) =>
        source switch
        {
            RuntimeValueSource.ContractReference => context.ContractReference,
            RuntimeValueSource.FileJobId => context.FileJobId,
            RuntimeValueSource.FileName => context.FileName,
            RuntimeValueSource.SourceRowId => context.SourceRowId,
            RuntimeValueSource.SourceRowNumber => context.SourceRowNumber,
            RuntimeValueSource.ConfigVersionReference => context.ConfigVersionReference,
            RuntimeValueSource.OutputKey => context.OutputKey,
            _ => throw new ArgumentOutOfRangeException(nameof(source))
        };

    private static bool TryConvert(object raw, TargetNode target, out object? value)
    {
        if (target.ScalarType == TargetScalarType.Text)
        {
            value = Convert.ToString(raw, CultureInfo.InvariantCulture);
            return true;
        }
        if (target.ScalarType == TargetScalarType.Integer &&
            raw is sbyte or byte or short or ushort or int or uint or long)
        {
            value = raw;
            return true;
        }
        if (target.ScalarType == TargetScalarType.Decimal &&
            raw is sbyte or byte or short or ushort or int or uint or long or float or double or decimal)
        {
            value = Convert.ToDecimal(raw, CultureInfo.InvariantCulture);
            return true;
        }
        if (target.ScalarType == TargetScalarType.Boolean && raw is bool ||
            target.ScalarType == TargetScalarType.Guid && raw is Guid ||
            target.ScalarType == TargetScalarType.Date && raw is DateOnly ||
            target.ScalarType == TargetScalarType.Time && raw is TimeOnly ||
            target.ScalarType == TargetScalarType.DateTime && raw is DateTime or DateTimeOffset)
        {
            value = raw;
            return true;
        }
        value = null;
        return false;
    }

    private static void ValidateNode(
        TargetNode node,
        object? value,
        string path,
        ICollection<ContractDiagnostic> diagnostics)
    {
        if (value is null)
        {
            if (node.IsRequired && !node.IsNullable)
            {
                diagnostics.Add(new(path, "RequiredPathMissing", "Required contract path is missing."));
            }
            return;
        }

        if (node.Kind == TargetNodeKind.Object)
        {
            if (value is not IReadOnlyDictionary<string, object?> dictionary)
            {
                diagnostics.Add(new(path, "ContractTypeMismatch", "Expected an object."));
                return;
            }
            foreach (var child in node.Properties)
            {
                dictionary.TryGetValue(child.Name, out var childValue);
                ValidateNode(child, childValue, path == "$" ? child.Name : $"{path}.{child.Name}", diagnostics);
            }
            var allowed = node.Properties.Select(child => child.Name)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var extra in dictionary.Keys.Where(key => !allowed.Contains(key)))
            {
                diagnostics.Add(new(
                    path == "$" ? extra : $"{path}.{extra}",
                    "AdditionalPropertyNotAllowed",
                    "Property is not declared by the contract schema."));
            }
            return;
        }

        if (node.Kind == TargetNodeKind.Array)
        {
            if (value is not IReadOnlyList<object?> list)
            {
                diagnostics.Add(new(path, "ContractTypeMismatch", "Expected an array."));
                return;
            }
            if (node.ItemSchema is not null)
            {
                for (var index = 0; index < list.Count; index++)
                {
                    ValidateNode(node.ItemSchema, list[index], $"{path}[{index}]", diagnostics);
                }
            }
            return;
        }

        if (!ScalarMatches(node.ScalarType, value))
        {
            diagnostics.Add(new(path, "ContractTypeMismatch", $"Value is incompatible with '{node.ScalarType}'."));
            return;
        }

        if (value is string text)
        {
            if (node.Constraints?.Length is int maximumLength && text.Length > maximumLength)
            {
                diagnostics.Add(new(path, "StringLengthExceeded", $"String length exceeds {maximumLength}."));
            }
            if (node.Constraints?.Pattern is string pattern &&
                !Regex.IsMatch(text, pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))
            {
                diagnostics.Add(new(path, "PatternMismatch", "String does not match the contract pattern."));
            }
        }
        if (value is IConvertible && node.ScalarType is TargetScalarType.Integer or TargetScalarType.Decimal)
        {
            var number = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            if (node.Constraints?.Minimum is decimal minimum && number < minimum)
            {
                diagnostics.Add(new(path, "MinimumViolation", $"Number is less than {minimum}."));
            }
            if (node.Constraints?.Maximum is decimal maximum && number > maximum)
            {
                diagnostics.Add(new(path, "MaximumViolation", $"Number is greater than {maximum}."));
            }
        }
    }

    private static bool ScalarMatches(TargetScalarType? type, object value) =>
        type switch
        {
            TargetScalarType.Text => value is string,
            TargetScalarType.Integer => value is sbyte or byte or short or ushort or int or uint or long,
            TargetScalarType.Decimal => value is sbyte or byte or short or ushort or int or uint or long or float or double or decimal,
            TargetScalarType.Boolean => value is bool,
            TargetScalarType.Date => value is DateOnly,
            TargetScalarType.Time => value is TimeOnly,
            TargetScalarType.DateTime => value is DateTime or DateTimeOffset,
            TargetScalarType.Guid => value is Guid,
            _ => false
        };
}

public sealed record PayloadBindingContext(
    string ContractReference,
    long FileJobId,
    long SourceRowId,
    int SourceRowNumber,
    string ConfigVersionReference,
    string OutputKey,
    string FileName = "");

public sealed record DeliveryPayload(
    IReadOnlyDictionary<string, object?> Values,
    string Json,
    string Hash);

public sealed record PayloadBindingResult(
    DeliveryPayload? Payload,
    IReadOnlyList<ContractDiagnostic> Diagnostics)
{
    public bool IsSuccess => Payload is not null;
}
