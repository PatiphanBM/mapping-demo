using System.Globalization;
using System.Text.Json;
using MappingDemo.Shared.Tables;

namespace MappingDemo.Shared.Transformation;

internal static class TransformationEvaluator
{
    private const string DefaultDateFormat = "yyyy-MM-dd";
    private const string DefaultDateTimeFormat = "yyyy-MM-ddTHH:mm:ss";

    private static readonly TimeZoneInfo AsiaBangkokTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById(
            TransformationCapabilities.AsiaBangkokTimeZone);

    public static async ValueTask<TransformationBatchResult> EvaluateAsync(
        CompiledTransformationProgram program,
        TransformationBatch batch,
        ITransformationLookupResolver? lookupResolver,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch.Rows);

        var lookupMatches = await ResolveLookupsAsync(
            program,
            batch.Rows,
            lookupResolver,
            cancellationToken);

        var results = new List<TransformationRowResult>(batch.Rows.Count);
        foreach (var row in batch.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(row);
            results.Add(EvaluateRow(program, row, lookupMatches));
        }

        return new TransformationBatchResult(results);
    }

    private static TransformationRowResult EvaluateRow(
        CompiledTransformationProgram program,
        TransformationSourceRow row,
        IReadOnlyDictionary<TransformationLookupRequest, IReadOnlySet<long>>
            lookupMatches)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(row.RowKey);
        ArgumentNullException.ThrowIfNull(row.SourceValues);
        ArgumentNullException.ThrowIfNull(row.MetadataValues);

        var context = new EvaluationContext(
            row,
            program.SemanticsProfile,
            lookupMatches);
        if (program.Definition.SourceWhen is not null)
        {
            try
            {
                if (EvaluateBoolean(
                        program.Definition.SourceWhen,
                        "sourceWhen",
                        context) is not true)
                {
                    return Filtered(row.RowKey);
                }
            }
            catch (EvaluationDataException exception)
            {
                return Invalid(
                    row.RowKey,
                    [ToRowError(exception, null, null)]);
            }
        }

        var outputs = new List<TransformationOutputResult>();
        var errors = new List<TransformationRowError>();

        for (var outputIndex = 0;
             outputIndex < program.Outputs.Count;
             outputIndex++)
        {
            var output = program.Outputs[outputIndex];
            var outputPath = $"outputs[{outputIndex}]";
            if (output.Definition.EmitWhen is not null)
            {
                try
                {
                    if (EvaluateBoolean(
                            output.Definition.EmitWhen,
                            $"{outputPath}.emitWhen",
                            context) is not true)
                    {
                        continue;
                    }
                }
                catch (EvaluationDataException exception)
                {
                    errors.Add(ToRowError(
                        exception,
                        output.Definition.OutputKey,
                        null));
                    continue;
                }
            }

            var values = new Dictionary<string, object?>(
                StringComparer.OrdinalIgnoreCase);
            for (var fieldIndex = 0;
                 fieldIndex < output.Fields.Count;
                 fieldIndex++)
            {
                var field = output.Fields[fieldIndex];
                var valuePath = $"{outputPath}.fields[{fieldIndex}].value";
                try
                {
                    var value = EvaluateValue(
                        field.Definition.Value,
                        valuePath,
                        context);
                    values.Add(
                        field.TargetColumn.Name,
                        ApplyAssignmentCoercion(field, value, valuePath));
                }
                catch (EvaluationDataException exception)
                {
                    errors.Add(ToRowError(
                        exception,
                        output.Definition.OutputKey,
                        field.TargetColumn.Name));
                }
            }

            outputs.Add(new TransformationOutputResult(
                output.Definition.OutputKey,
                values));
        }

        if (errors.Count > 0)
        {
            return Invalid(row.RowKey, errors);
        }

        return outputs.Count == 0
            ? Filtered(row.RowKey)
            : new TransformationRowResult(
                row.RowKey,
                TransformationRowStatus.Done,
                outputs,
                []);
    }

    private static async ValueTask<IReadOnlyDictionary<
        TransformationLookupRequest,
        IReadOnlySet<long>>> ResolveLookupsAsync(
            CompiledTransformationProgram program,
            IReadOnlyList<TransformationSourceRow> rows,
            ITransformationLookupResolver? lookupResolver,
            CancellationToken cancellationToken)
    {
        if (program.LookupDependencies.Count == 0)
        {
            return new Dictionary<
                TransformationLookupRequest,
                IReadOnlySet<long>>();
        }

        if (lookupResolver is null)
        {
            throw new InvalidOperationException(
                "The compiled transformation requires a lookup resolver.");
        }

        var requests = new HashSet<TransformationLookupRequest>();
        var noMatches = new Dictionary<
            TransformationLookupRequest,
            IReadOnlySet<long>>();
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(row);
            var context = new EvaluationContext(
                row,
                program.SemanticsProfile,
                noMatches);

            foreach (var dependency in program.LookupDependencies)
            {
                try
                {
                    var request = BuildLookupRequest(
                        dependency.Capability,
                        dependency.Arguments,
                        dependency.Path,
                        context);
                    if (request is not null)
                    {
                        requests.Add(request);
                    }
                }
                catch (EvaluationDataException)
                {
                    // Reachable evaluation below reports the row-scoped error.
                }
            }
        }

        if (requests.Count == 0)
        {
            return noMatches;
        }

        var matches = await lookupResolver.ResolveAsync(
            requests.ToArray(),
            cancellationToken);
        var requested = requests.ToHashSet();
        return matches
            .Where(match => requested.Contains(match.Request))
            .GroupBy(match => match.Request)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlySet<long>)group
                    .Select(match => match.SourceRowId)
                    .ToHashSet());
    }

    private static object? ApplyAssignmentCoercion(
        CompiledFieldAssignment field,
        object? value,
        string path)
    {
        if (field.Coercion == TargetAssignmentCoercion.None)
        {
            if (value is null && field.TargetColumn.IsRequired)
            {
                throw new EvaluationDataException(path, "Required");
            }

            return value;
        }

        var text = (string?)value;
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            if (field.TargetColumn.IsRequired)
            {
                throw new EvaluationDataException(path, "Required");
            }

            return null;
        }

        return field.Coercion == TargetAssignmentCoercion.TrimText
            ? trimmed
            : ConvertText(
                trimmed,
                ToExpressionType(field.TargetColumn.DataType),
                field.Definition.InputFormat,
                path);
    }

    private static object? EvaluateValue(
        ValueExpression expression,
        string path,
        EvaluationContext context)
    {
        return expression switch
        {
            SourceExpression source => context.GetSource(source.Field),
            MetadataExpression metadata => context.GetMetadata(metadata.Name),
            LiteralExpression literal => EvaluateLiteral(literal),
            CoalesceExpression coalesce => EvaluateCoalesce(
                coalesce,
                path,
                context),
            DefaultIfBlankExpression defaultIfBlank => EvaluateDefaultIfBlank(
                defaultIfBlank,
                path,
                context),
            TrimExpression trim => ((string?)EvaluateValue(
                trim.Value,
                $"{path}.value",
                context))?.Trim(),
            CaseExpression caseExpression => EvaluateCase(
                caseExpression,
                path,
                context),
            SubstringExpression substring => EvaluateSubstring(
                substring,
                path,
                context),
            ConvertExpression convert => EvaluateConvert(
                convert,
                path,
                context),
            CurrentDateExpression => context.GetCurrentDate(),
            CallExpression call => EvaluateCall(call, path, context),
            _ => throw new InvalidOperationException(
                $"Unsupported value expression '{expression.GetType().Name}'.")
        };
    }

    private static object? EvaluateLiteral(LiteralExpression literal)
    {
        if (literal.Value.ValueKind is JsonValueKind.Null
            or JsonValueKind.Undefined)
        {
            return null;
        }

        return literal.Type switch
        {
            ExpressionValueType.Text => literal.Value.GetString(),
            ExpressionValueType.Decimal => literal.Value.GetDecimal(),
            ExpressionValueType.Integer => literal.Value.GetInt32(),
            ExpressionValueType.Date => DateOnly.ParseExact(
                literal.Value.GetString()!,
                DefaultDateFormat,
                CultureInfo.InvariantCulture),
            ExpressionValueType.DateTime => ParseDateTimeLiteral(
                literal.Value.GetString()!),
            ExpressionValueType.Boolean => literal.Value.GetBoolean(),
            ExpressionValueType.Guid => Guid.Parse(literal.Value.GetString()!),
            _ => throw new InvalidOperationException(
                $"Unsupported literal type '{literal.Type}'.")
        };
    }

    private static object? EvaluateCoalesce(
        CoalesceExpression expression,
        string path,
        EvaluationContext context)
    {
        for (var index = 0; index < expression.Values.Count; index++)
        {
            var value = EvaluateValue(
                expression.Values[index],
                $"{path}.values[{index}]",
                context);
            if (value is not null)
            {
                return value;
            }
        }

        return null;
    }

    private static object? EvaluateDefaultIfBlank(
        DefaultIfBlankExpression expression,
        string path,
        EvaluationContext context)
    {
        var value = (string?)EvaluateValue(
            expression.Value,
            $"{path}.value",
            context);
        return value is null || value.Trim().Length == 0
            ? EvaluateValue(
                expression.Fallback,
                $"{path}.fallback",
                context)
            : value;
    }

    private static object? EvaluateCase(
        CaseExpression expression,
        string path,
        EvaluationContext context)
    {
        for (var index = 0; index < expression.Cases.Count; index++)
        {
            var arm = expression.Cases[index];
            var armPath = $"{path}.cases[{index}]";
            if (EvaluateBoolean(arm.When, $"{armPath}.when", context) is true)
            {
                return EvaluateValue(arm.Then, $"{armPath}.then", context);
            }
        }

        return EvaluateValue(expression.Else, $"{path}.else", context);
    }

    private static object? EvaluateSubstring(
        SubstringExpression expression,
        string path,
        EvaluationContext context)
    {
        var value = (string?)EvaluateValue(
            expression.Value,
            $"{path}.value",
            context);
        if (value is null)
        {
            return null;
        }

        var startIndex = expression.Start - 1;
        if (startIndex >= value.Length || expression.Length == 0)
        {
            return string.Empty;
        }

        return value.Substring(
            startIndex,
            Math.Min(expression.Length, value.Length - startIndex));
    }

    private static object? EvaluateConvert(
        ConvertExpression expression,
        string path,
        EvaluationContext context)
    {
        var value = (string?)EvaluateValue(
            expression.Value,
            $"{path}.value",
            context);
        return value is null
            ? null
            : ConvertText(value, expression.Type, expression.InputFormat, path);
    }

    private static object? EvaluateCall(
        CallExpression expression,
        string path,
        EvaluationContext context)
    {
        if (!string.Equals(
            expression.Function,
            TransformationCapabilities.FormatRegNoV1,
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Unsupported compiled function '{expression.Function}'.");
        }

        var value = (string?)EvaluateValue(
            expression.Arguments[0],
            $"{path}.arguments[0]",
            context);
        return value?.Trim();
    }

    private static bool? EvaluateBoolean(
        BooleanExpression expression,
        string path,
        EvaluationContext context)
    {
        return expression switch
        {
            EqualExpression equal => EvaluateEquality(
                equal.Left,
                equal.Right,
                path,
                context),
            NotEqualExpression notEqual => Negate(EvaluateEquality(
                notEqual.Left,
                notEqual.Right,
                path,
                context)),
            GreaterThanExpression greaterThan => EvaluateOrderedComparison(
                greaterThan.Left,
                greaterThan.Right,
                path,
                context,
                comparison => comparison > 0),
            GreaterThanOrEqualExpression greaterThanOrEqual =>
                EvaluateOrderedComparison(
                    greaterThanOrEqual.Left,
                    greaterThanOrEqual.Right,
                    path,
                    context,
                    comparison => comparison >= 0),
            LessThanExpression lessThan => EvaluateOrderedComparison(
                lessThan.Left,
                lessThan.Right,
                path,
                context,
                comparison => comparison < 0),
            LessThanOrEqualExpression lessThanOrEqual =>
                EvaluateOrderedComparison(
                    lessThanOrEqual.Left,
                    lessThanOrEqual.Right,
                    path,
                    context,
                    comparison => comparison <= 0),
            IsNullExpression isNull => EvaluateValue(
                isNull.Value,
                $"{path}.value",
                context) is null,
            IsBlankExpression isBlank => IsBlank(EvaluateValue(
                isBlank.Value,
                $"{path}.value",
                context)),
            NotBlankExpression notBlank => !IsBlank(EvaluateValue(
                notBlank.Value,
                $"{path}.value",
                context)),
            LikeExpression like => EvaluateLike(like, path, context),
            InExpression inExpression => EvaluateIn(
                inExpression.Value,
                inExpression.Values,
                path,
                context),
            NotInExpression notInExpression => Negate(EvaluateIn(
                notInExpression.Value,
                notInExpression.Values,
                path,
                context)),
            AndExpression andExpression => EvaluateAnd(
                andExpression,
                path,
                context),
            OrExpression orExpression => EvaluateOr(
                orExpression,
                path,
                context),
            NotExpression notExpression => Negate(EvaluateBoolean(
                notExpression.Value,
                $"{path}.value",
                context)),
            ExistsExpression exists => EvaluateLookup(
                exists.Lookup,
                exists.Arguments,
                path,
                context),
            NotExistsExpression notExists => !EvaluateLookup(
                notExists.Lookup,
                notExists.Arguments,
                path,
                context),
            _ => throw new InvalidOperationException(
                $"Unsupported boolean expression '{expression.GetType().Name}'.")
        };
    }

    private static bool EvaluateLookup(
        string capability,
        IReadOnlyDictionary<string, ValueExpression> arguments,
        string path,
        EvaluationContext context)
    {
        var request = BuildLookupRequest(
            capability,
            arguments,
            path,
            context);
        if (request is null)
        {
            return false;
        }

        var currentSourceRowId = context.SourceRowId
            ?? throw new InvalidOperationException(
                $"Lookup '{capability}' requires currentSourceRowId for row " +
                $"'{context.RowKey}'.");
        return context.LookupMatches.TryGetValue(request, out var sourceRowIds)
            && sourceRowIds.Any(sourceRowId => sourceRowId != currentSourceRowId);
    }

    private static TransformationLookupRequest? BuildLookupRequest(
        string capability,
        IReadOnlyDictionary<string, ValueExpression> arguments,
        string path,
        EvaluationContext context)
    {
        if (!string.Equals(
            capability,
            TransformationCapabilities.MtiOriginalPolicyExistsV1,
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Unsupported compiled lookup '{capability}'.");
        }

        var systemId = (string?)EvaluateValue(
            arguments["systemId"],
            $"{path}.arguments.systemId",
            context);
        var productName = (string?)EvaluateValue(
            arguments["productName"],
            $"{path}.arguments.productName",
            context);
        var loanNumber = (string?)EvaluateValue(
            arguments["loanNumber"],
            $"{path}.arguments.loanNumber",
            context);
        if (systemId is null || productName is null || loanNumber is null)
        {
            return null;
        }

        return new TransformationLookupRequest(
            capability,
            systemId,
            productName,
            loanNumber);
    }

    private static bool? EvaluateEquality(
        ValueExpression leftExpression,
        ValueExpression rightExpression,
        string path,
        EvaluationContext context)
    {
        var left = EvaluateValue(leftExpression, $"{path}.left", context);
        var right = EvaluateValue(rightExpression, $"{path}.right", context);
        if (left is null || right is null)
        {
            return null;
        }

        return ValuesEqual(left, right);
    }

    private static bool? EvaluateOrderedComparison(
        ValueExpression leftExpression,
        ValueExpression rightExpression,
        string path,
        EvaluationContext context,
        Func<int, bool> predicate)
    {
        var left = EvaluateValue(leftExpression, $"{path}.left", context);
        var right = EvaluateValue(rightExpression, $"{path}.right", context);
        if (left is null || right is null)
        {
            return null;
        }

        return predicate(CompareValues(left, right));
    }

    private static bool? EvaluateLike(
        LikeExpression expression,
        string path,
        EvaluationContext context)
    {
        var value = (string?)EvaluateValue(
            expression.Value,
            $"{path}.value",
            context);
        var pattern = (string?)EvaluateValue(
            expression.Pattern,
            $"{path}.pattern",
            context);
        return value is null || pattern is null
            ? null
            : SqlLikeMatcher.IsMatch(value, pattern);
    }

    private static bool? EvaluateIn(
        ValueExpression valueExpression,
        IReadOnlyList<ValueExpression> candidates,
        string path,
        EvaluationContext context)
    {
        var value = EvaluateValue(
            valueExpression,
            $"{path}.value",
            context);
        if (value is null)
        {
            return null;
        }

        var foundNull = false;
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = EvaluateValue(
                candidates[index],
                $"{path}.values[{index}]",
                context);
            if (candidate is null)
            {
                foundNull = true;
            }
            else if (ValuesEqual(value, candidate))
            {
                return true;
            }
        }

        return foundNull ? null : false;
    }

    private static bool? EvaluateAnd(
        AndExpression expression,
        string path,
        EvaluationContext context)
    {
        var foundNull = false;
        for (var index = 0; index < expression.Operands.Count; index++)
        {
            var value = EvaluateBoolean(
                expression.Operands[index],
                $"{path}.operands[{index}]",
                context);
            if (value is false)
            {
                return false;
            }

            foundNull |= value is null;
        }

        return foundNull ? null : true;
    }

    private static bool? EvaluateOr(
        OrExpression expression,
        string path,
        EvaluationContext context)
    {
        var foundNull = false;
        for (var index = 0; index < expression.Operands.Count; index++)
        {
            var value = EvaluateBoolean(
                expression.Operands[index],
                $"{path}.operands[{index}]",
                context);
            if (value is true)
            {
                return true;
            }

            foundNull |= value is null;
        }

        return foundNull ? null : false;
    }

    private static object ConvertText(
        string value,
        ExpressionValueType targetType,
        string? inputFormat,
        string path)
    {
        return targetType switch
        {
            ExpressionValueType.Decimal => ParseDecimal(value, path),
            ExpressionValueType.Integer => ParseInteger(value, path),
            ExpressionValueType.Date => ParseDate(
                value,
                inputFormat ?? DefaultDateFormat,
                path),
            ExpressionValueType.DateTime => ParseDateTime(
                value,
                inputFormat ?? DefaultDateTimeFormat,
                path),
            ExpressionValueType.Boolean => ParseBoolean(value, path),
            ExpressionValueType.Guid => ParseGuid(value, path),
            _ => throw new InvalidOperationException(
                $"Text conversion to '{targetType}' is not supported.")
        };
    }

    private static decimal ParseDecimal(string value, string path)
    {
        const NumberStyles styles =
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
        if (decimal.TryParse(
            value,
            styles,
            CultureInfo.InvariantCulture,
            out var result))
        {
            return result;
        }

        throw new EvaluationDataException(path, "InvalidDecimal");
    }

    private static int ParseInteger(string value, string path)
    {
        if (int.TryParse(
            value,
            NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out var result))
        {
            return result;
        }

        throw new EvaluationDataException(path, "InvalidInteger");
    }

    private static DateOnly ParseDate(
        string value,
        string format,
        string path)
    {
        if (DateOnly.TryParseExact(
            value,
            format,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var result))
        {
            return result;
        }

        throw new EvaluationDataException(path, "InvalidDate");
    }

    private static DateTime ParseDateTime(
        string value,
        string format,
        string path)
    {
        if (DateTime.TryParseExact(
            value,
            format,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var result))
        {
            return DateTime.SpecifyKind(result, DateTimeKind.Unspecified);
        }

        throw new EvaluationDataException(path, "InvalidDateTime");
    }

    private static bool ParseBoolean(string value, string path)
    {
        if (value == "1")
        {
            return true;
        }

        if (value == "0")
        {
            return false;
        }

        if (bool.TryParse(value, out var result))
        {
            return result;
        }

        throw new EvaluationDataException(path, "InvalidBoolean");
    }

    private static Guid ParseGuid(string value, string path)
    {
        if (Guid.TryParse(value, out var result))
        {
            return result;
        }

        throw new EvaluationDataException(path, "InvalidGuid");
    }

    private static DateTime ParseDateTimeLiteral(string value)
    {
        if (DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var result))
        {
            return DateTime.SpecifyKind(result, DateTimeKind.Unspecified);
        }

        throw new InvalidOperationException(
            "A compiled DateTime literal could not be parsed.");
    }

    private static bool IsBlank(object? value)
    {
        return value is null || ((string)value).Trim().Length == 0;
    }

    private static bool ValuesEqual(object left, object right)
    {
        return left is string leftText && right is string rightText
            ? string.Equals(
                leftText,
                rightText,
                StringComparison.OrdinalIgnoreCase)
            : left.Equals(right);
    }

    private static int CompareValues(object left, object right)
    {
        if (left is string leftText && right is string rightText)
        {
            return string.Compare(
                leftText,
                rightText,
                StringComparison.OrdinalIgnoreCase);
        }

        return left switch
        {
            decimal leftDecimal => leftDecimal.CompareTo((decimal)right),
            int leftInteger => leftInteger.CompareTo((int)right),
            DateOnly leftDate => leftDate.CompareTo((DateOnly)right),
            DateTime leftDateTime => leftDateTime.CompareTo((DateTime)right),
            _ => throw new InvalidOperationException(
                $"Values of type '{left.GetType().Name}' cannot be ordered.")
        };
    }

    private static bool? Negate(bool? value)
    {
        return value.HasValue ? !value.Value : null;
    }

    private static ExpressionValueType ToExpressionType(
        ColumnDataType dataType)
    {
        return dataType switch
        {
            ColumnDataType.Text => ExpressionValueType.Text,
            ColumnDataType.Decimal => ExpressionValueType.Decimal,
            ColumnDataType.Integer => ExpressionValueType.Integer,
            ColumnDataType.Date => ExpressionValueType.Date,
            ColumnDataType.DateTime => ExpressionValueType.DateTime,
            ColumnDataType.Boolean => ExpressionValueType.Boolean,
            ColumnDataType.Guid => ExpressionValueType.Guid,
            _ => throw new ArgumentOutOfRangeException(nameof(dataType))
        };
    }

    private static TransformationRowError ToRowError(
        EvaluationDataException exception,
        string? outputKey,
        string? field)
    {
        return new TransformationRowError(
            outputKey,
            field,
            exception.ExpressionPath,
            exception.Reason);
    }

    private static TransformationRowResult Filtered(string rowKey)
    {
        return new TransformationRowResult(
            rowKey,
            TransformationRowStatus.Filtered,
            [],
            []);
    }

    private static TransformationRowResult Invalid(
        string rowKey,
        IReadOnlyList<TransformationRowError> errors)
    {
        return new TransformationRowResult(
            rowKey,
            TransformationRowStatus.Invalid,
            [],
            errors);
    }

    private sealed class EvaluationContext
    {
        private readonly TransformationSourceRow _row;
        private readonly string _semanticsProfile;

        public EvaluationContext(
            TransformationSourceRow row,
            string semanticsProfile,
            IReadOnlyDictionary<
                TransformationLookupRequest,
                IReadOnlySet<long>> lookupMatches)
        {
            _row = row;
            _semanticsProfile = semanticsProfile;
            LookupMatches = lookupMatches;
        }

        public string RowKey => _row.RowKey;

        public long? SourceRowId => _row.SourceRowId;

        public IReadOnlyDictionary<
            TransformationLookupRequest,
            IReadOnlySet<long>>
            LookupMatches
        { get; }

        public string? GetSource(string field)
        {
            if (_row.SourceValues.TryGetValue(field, out var value))
            {
                return value;
            }

            var match = _row.SourceValues.FirstOrDefault(pair =>
                string.Equals(
                    pair.Key,
                    field,
                    StringComparison.OrdinalIgnoreCase));
            if (match.Key is not null)
            {
                return match.Value;
            }

            throw new InvalidOperationException(
                $"Compiled source field '{field}' is missing from row '{_row.RowKey}'.");
        }

        public object? GetMetadata(string name)
        {
            if (name == "evaluationTime")
            {
                return GetEvaluationTime();
            }

            if (!_row.MetadataValues.TryGetValue(name, out var value))
            {
                throw new InvalidOperationException(
                    $"Metadata '{name}' is missing from row '{_row.RowKey}'.");
            }

            return name switch
            {
                "fileName" => value as string
                    ?? throw InvalidMetadata(name),
                "rowNumber" or "physicalLineNumber" => value switch
                {
                    int integer => integer,
                    long longInteger when longInteger is >= int.MinValue
                        and <= int.MaxValue => (int)longInteger,
                    _ => throw InvalidMetadata(name)
                },
                _ => throw new InvalidOperationException(
                    $"Unsupported compiled metadata '{name}'.")
            };
        }

        public DateOnly GetCurrentDate()
        {
            return DateOnly.FromDateTime(GetEvaluationTime());
        }

        private DateTime GetEvaluationTime()
        {
            if (!string.Equals(
                _semanticsProfile,
                TransformationCapabilities.SqlServerLegacyProfile,
                StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Unsupported compiled semantics profile '{_semanticsProfile}'.");
            }

            return TimeZoneInfo.ConvertTime(
                _row.EvaluationAt,
                AsiaBangkokTimeZone).DateTime;
        }

        private InvalidOperationException InvalidMetadata(string name)
        {
            return new InvalidOperationException(
                $"Metadata '{name}' has an invalid runtime type in row '{_row.RowKey}'.");
        }
    }

    private sealed class EvaluationDataException : Exception
    {
        public EvaluationDataException(string expressionPath, string reason)
            : base(reason)
        {
            ExpressionPath = expressionPath;
            Reason = reason;
        }

        public string ExpressionPath { get; }

        public string Reason { get; }
    }

    private static class SqlLikeMatcher
    {
        public static bool IsMatch(string value, string pattern)
        {
            var tokens = Tokenize(pattern);
            var next = new bool[value.Length + 1];
            next[value.Length] = true;

            for (var tokenIndex = tokens.Count - 1;
                 tokenIndex >= 0;
                 tokenIndex--)
            {
                var token = tokens[tokenIndex];
                var current = new bool[value.Length + 1];
                if (token is PercentToken)
                {
                    current[value.Length] = next[value.Length];
                    for (var valueIndex = value.Length - 1;
                         valueIndex >= 0;
                         valueIndex--)
                    {
                        current[valueIndex] = next[valueIndex]
                            || current[valueIndex + 1];
                    }
                }
                else
                {
                    for (var valueIndex = value.Length - 1;
                         valueIndex >= 0;
                         valueIndex--)
                    {
                        current[valueIndex] = token.Matches(value[valueIndex])
                            && next[valueIndex + 1];
                    }
                }

                next = current;
            }

            return next[0];
        }

        private static IReadOnlyList<LikeToken> Tokenize(string pattern)
        {
            var tokens = new List<LikeToken>();
            for (var index = 0; index < pattern.Length; index++)
            {
                if (pattern[index] == '%')
                {
                    if (tokens.Count == 0 || tokens[^1] is not PercentToken)
                    {
                        tokens.Add(new PercentToken());
                    }

                    continue;
                }

                if (pattern[index] == '_')
                {
                    tokens.Add(new AnyCharacterToken());
                    continue;
                }

                if (pattern[index] == '['
                    && TryReadCharacterClass(
                        pattern,
                        ref index,
                        out var characterClass))
                {
                    tokens.Add(characterClass);
                    continue;
                }

                tokens.Add(new LiteralToken(pattern[index]));
            }

            return tokens;
        }

        private static bool TryReadCharacterClass(
            string pattern,
            ref int index,
            out CharacterClassToken token)
        {
            var closeIndex = pattern.IndexOf(']', index + 1);
            if (closeIndex < 0 || closeIndex == index + 1)
            {
                token = null!;
                return false;
            }

            var itemIndex = index + 1;
            var negated = pattern[itemIndex] == '^';
            if (negated)
            {
                itemIndex++;
            }

            if (itemIndex >= closeIndex)
            {
                token = null!;
                return false;
            }

            var ranges = new List<CharacterRange>();
            while (itemIndex < closeIndex)
            {
                var start = pattern[itemIndex];
                if (itemIndex + 2 < closeIndex
                    && pattern[itemIndex + 1] == '-')
                {
                    ranges.Add(new CharacterRange(
                        start,
                        pattern[itemIndex + 2]));
                    itemIndex += 3;
                }
                else
                {
                    ranges.Add(new CharacterRange(start, start));
                    itemIndex++;
                }
            }

            token = new CharacterClassToken(negated, ranges);
            index = closeIndex;
            return true;
        }

        private abstract record LikeToken
        {
            public abstract bool Matches(char value);
        }

        private sealed record PercentToken : LikeToken
        {
            public override bool Matches(char value) => true;
        }

        private sealed record AnyCharacterToken : LikeToken
        {
            public override bool Matches(char value) => true;
        }

        private sealed record LiteralToken(char Value) : LikeToken
        {
            public override bool Matches(char value)
            {
                return CharactersEqual(Value, value);
            }
        }

        private sealed record CharacterClassToken(
            bool Negated,
            IReadOnlyList<CharacterRange> Ranges) : LikeToken
        {
            public override bool Matches(char value)
            {
                var normalized = char.ToUpperInvariant(value);
                var contains = Ranges.Any(range =>
                    normalized >= char.ToUpperInvariant(range.Start)
                    && normalized <= char.ToUpperInvariant(range.End));
                return Negated ? !contains : contains;
            }
        }

        private readonly record struct CharacterRange(char Start, char End);

        private static bool CharactersEqual(char left, char right)
        {
            return string.Equals(
                left.ToString(),
                right.ToString(),
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
