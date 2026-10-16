using System.Globalization;
using System.Text.Json;
using MappingDemo.Shared.Tables;

namespace MappingDemo.Shared.Transformation;

internal static class TransformationCompiler
{
    private const int DefaultLanguageVersion = 1;

    public static TransformationCompilationResult Compile(
        TransformationProgram program,
        TransformationSchema schema)
    {
        var context = new CompilerContext(program, schema);
        return context.Compile();
    }

    private sealed class CompilerContext
    {
        private const int MaxOutputs = TransformationCapabilities.MaxOutputs;
        private const int MaxFieldsPerOutput =
            TransformationCapabilities.MaxFieldsPerOutput;
        private const int MaxCollectionItems =
            TransformationCapabilities.MaxCollectionItems;
        private const int MaxExpressionDepth =
            TransformationCapabilities.MaxExpressionDepth;
        private const int MaxExpressionNodes =
            TransformationCapabilities.MaxExpressionNodes;

        private static readonly IReadOnlyDictionary<string, InferredExpressionType>
            MetadataCatalog = new Dictionary<string, InferredExpressionType>(
                StringComparer.Ordinal)
            {
                ["fileName"] = new(ExpressionValueType.Text, false),
                ["rowNumber"] = new(ExpressionValueType.Integer, false),
                ["physicalLineNumber"] = new(ExpressionValueType.Integer, false),
                ["evaluationTime"] = new(ExpressionValueType.DateTime, false)
            };

        private static readonly string[] MtiLookupArguments =
            ["systemId", "productName", "loanNumber"];

        private readonly TransformationProgram _program;
        private readonly TransformationSchema _schema;
        private readonly List<TransformationDiagnostic> _diagnostics = [];
        private readonly HashSet<string> _sourceDependencies =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly List<CompiledLookupDependency> _lookupDependencies = [];
        private readonly Dictionary<string, ColumnDefinition> _sourceColumns;
        private readonly Dictionary<string, ColumnDefinition> _targetColumns;
        private int _expressionNodes;
        private bool _nodeLimitReported;

        public CompilerContext(
            TransformationProgram program,
            TransformationSchema schema)
        {
            _program = program;
            _schema = schema;
            _sourceColumns = schema.SourceTable.Columns.ToDictionary(
                column => column.Name,
                StringComparer.OrdinalIgnoreCase);
            _targetColumns = schema.NormalizedTable.Columns.ToDictionary(
                column => column.Name,
                StringComparer.OrdinalIgnoreCase);
        }

        public TransformationCompilationResult Compile()
        {
            var languageVersion = _program.LanguageVersion
                ?? DefaultLanguageVersion;
            ValidateLanguageAndProfile(languageVersion);

            if (_diagnostics.Count > 0)
            {
                return Failure();
            }

            var compiledOutputs = CompileProgram();
            if (_diagnostics.Count > 0)
            {
                return Failure();
            }

            var compiled = new CompiledTransformationProgram(
                _program,
                _schema,
                languageVersion,
                _program.SemanticsProfile,
                compiledOutputs,
                new HashSet<string>(
                    _sourceDependencies,
                    StringComparer.OrdinalIgnoreCase),
                _lookupDependencies.ToArray());

            return new TransformationCompilationResult(compiled, _diagnostics);
        }

        private void ValidateLanguageAndProfile(int languageVersion)
        {
            if (languageVersion != DefaultLanguageVersion)
            {
                AddError(
                    "languageVersion",
                    "UnknownLanguageVersion",
                    $"Language version '{languageVersion}' is not supported.");
            }

            if (string.IsNullOrWhiteSpace(_program.SemanticsProfile))
            {
                AddError(
                    "semanticsProfile",
                    "SemanticsProfileRequired",
                    "A semantics profile is required.");
            }
            else if (!TransformationCapabilities.SemanticsProfiles.Contains(
                         _program.SemanticsProfile,
                         StringComparer.Ordinal))
            {
                AddError(
                    "semanticsProfile",
                    "UnknownSemanticsProfile",
                    $"Semantics profile '{_program.SemanticsProfile}' is not supported.");
            }
        }

        private IReadOnlyList<CompiledTransformationOutput> CompileProgram()
        {
            if (_program.SourceWhen is not null)
            {
                CompileBoolean(_program.SourceWhen, "sourceWhen", 0);
            }

            if (_program.Outputs is null || _program.Outputs.Count == 0)
            {
                AddError("outputs", "OutputsRequired", "At least one output is required.");
                return [];
            }

            if (_program.Outputs.Count > MaxOutputs)
            {
                AddError(
                    "outputs",
                    "OutputLimitExceeded",
                    $"A program may contain at most {MaxOutputs} outputs.");
            }

            var outputKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var compiledOutputs = new List<CompiledTransformationOutput>(
                _program.Outputs.Count);

            for (var outputIndex = 0;
                 outputIndex < _program.Outputs.Count;
                 outputIndex++)
            {
                var output = _program.Outputs[outputIndex];
                var outputPath = $"outputs[{outputIndex}]";

                if (output is null)
                {
                    AddError(
                        outputPath,
                        "OutputRequired",
                        "An output definition is required.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(output.OutputKey))
                {
                    AddError(
                        $"{outputPath}.outputKey",
                        "OutputKeyRequired",
                        "An output key is required.");
                }
                else if (!outputKeys.Add(output.OutputKey))
                {
                    AddError(
                        $"{outputPath}.outputKey",
                        "DuplicateOutputKey",
                        $"Output key '{output.OutputKey}' is duplicated.");
                }

                if (output.EmitWhen is not null)
                {
                    CompileBoolean(
                        output.EmitWhen,
                        $"{outputPath}.emitWhen",
                        0);
                }

                compiledOutputs.Add(CompileOutput(output, outputPath));
            }

            return compiledOutputs;
        }

        private CompiledTransformationOutput CompileOutput(
            TransformationOutput output,
            string outputPath)
        {
            if (output.Fields is null)
            {
                AddError(
                    $"{outputPath}.fields",
                    "FieldsRequired",
                    "The output fields collection is required.");
                return new CompiledTransformationOutput(output, []);
            }

            if (output.Fields.Count > MaxFieldsPerOutput)
            {
                AddError(
                    $"{outputPath}.fields",
                    "FieldLimitExceeded",
                    $"An output may contain at most {MaxFieldsPerOutput} fields.");
            }

            var assignedTargets = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            var compiledFields = new List<CompiledFieldAssignment>(
                output.Fields.Count);

            for (var fieldIndex = 0;
                 fieldIndex < output.Fields.Count;
                 fieldIndex++)
            {
                var assignment = output.Fields[fieldIndex];
                var fieldPath = $"{outputPath}.fields[{fieldIndex}]";
                if (assignment is null)
                {
                    AddError(
                        fieldPath,
                        "FieldAssignmentRequired",
                        "A field assignment is required.");
                    continue;
                }

                ColumnDefinition? targetColumn = null;
                if (string.IsNullOrWhiteSpace(assignment.NormalizedColumn))
                {
                    AddError(
                        $"{fieldPath}.normalizedColumn",
                        "TargetRequired",
                        "A normalized target column is required.");
                }
                else if (!assignedTargets.Add(assignment.NormalizedColumn))
                {
                    AddError(
                        $"{fieldPath}.normalizedColumn",
                        "DuplicateTarget",
                        $"Target column '{assignment.NormalizedColumn}' is assigned more than once.");
                }
                else if (!_targetColumns.TryGetValue(
                    assignment.NormalizedColumn,
                    out targetColumn))
                {
                    AddError(
                        $"{fieldPath}.normalizedColumn",
                        "UnknownTargetColumn",
                        $"Target column '{assignment.NormalizedColumn}' does not exist.");
                }

                var valueType = assignment.Value is null
                    ? MissingValue($"{fieldPath}.value")
                    : CompileValue(assignment.Value, $"{fieldPath}.value", 0);

                if (targetColumn is not null && valueType is not null)
                {
                    var coercion = CompileAssignment(
                        assignment,
                        targetColumn,
                        valueType.Value,
                        fieldPath);
                    if (coercion is not null)
                    {
                        compiledFields.Add(
                            new CompiledFieldAssignment(
                                assignment,
                                targetColumn,
                                valueType.Value,
                                coercion.Value));
                    }
                }
            }

            foreach (var requiredColumn in _targetColumns.Values
                .Where(column => column.IsRequired))
            {
                if (!assignedTargets.Contains(requiredColumn.Name))
                {
                    AddError(
                        $"{outputPath}.fields",
                        "RequiredTargetMissing",
                        $"Required target column '{requiredColumn.Name}' is not assigned.");
                }
            }

            return new CompiledTransformationOutput(output, compiledFields);
        }

        private TargetAssignmentCoercion? CompileAssignment(
            FieldAssignment assignment,
            ColumnDefinition targetColumn,
            InferredExpressionType valueType,
            string fieldPath)
        {
            var targetType = ToExpressionType(targetColumn.DataType);
            ValidateAssignmentInputFormat(
                assignment.InputFormat,
                valueType.Type,
                targetType,
                $"{fieldPath}.inputFormat");

            if (valueType.Type == targetType)
            {
                return targetType == ExpressionValueType.Text
                    ? TargetAssignmentCoercion.TrimText
                    : TargetAssignmentCoercion.None;
            }

            if (valueType.Type == ExpressionValueType.Text)
            {
                return TargetAssignmentCoercion.TrimAndConvert;
            }

            AddError(
                $"{fieldPath}.value",
                "AssignmentTypeMismatch",
                $"A value of type '{valueType.Type}' cannot be assigned to target type '{targetType}'.");
            return null;
        }

        private void ValidateAssignmentInputFormat(
            string? inputFormat,
            ExpressionValueType valueType,
            ExpressionValueType targetType,
            string path)
        {
            if (inputFormat is null)
            {
                return;
            }

            if (targetType is not ExpressionValueType.Date
                and not ExpressionValueType.DateTime)
            {
                AddError(
                    path,
                    "InputFormatNotSupported",
                    "inputFormat is supported only for Date and DateTime targets.");
                return;
            }

            if (valueType != ExpressionValueType.Text)
            {
                AddError(
                    path,
                    "InputFormatRequiresText",
                    "inputFormat may be used only when the assigned value is Text.");
                return;
            }

            ValidateKnownInputFormat(inputFormat, targetType, path);
        }

        private InferredExpressionType? CompileValue(
            ValueExpression expression,
            string path,
            int depth)
        {
            if (!EnterExpression(path, depth))
            {
                return null;
            }

            return expression switch
            {
                SourceExpression source => CompileSource(source, path),
                MetadataExpression metadata => CompileMetadata(metadata, path),
                LiteralExpression literal => CompileLiteral(literal, path),
                CoalesceExpression coalesce => CompileCoalesce(
                    coalesce,
                    path,
                    depth),
                DefaultIfBlankExpression defaultIfBlank =>
                    CompileDefaultIfBlank(defaultIfBlank, path, depth),
                TrimExpression trim => CompileTrim(trim, path, depth),
                CaseExpression caseExpression => CompileCase(
                    caseExpression,
                    path,
                    depth),
                SubstringExpression substring => CompileSubstring(
                    substring,
                    path,
                    depth),
                ConvertExpression convert => CompileConvert(
                    convert,
                    path,
                    depth),
                CurrentDateExpression => new InferredExpressionType(
                    ExpressionValueType.Date,
                    false),
                CallExpression call => CompileCall(call, path, depth),
                _ => UnknownExpression(path)
            };
        }

        private InferredExpressionType? CompileSource(
            SourceExpression source,
            string path)
        {
            if (string.IsNullOrWhiteSpace(source.Field)
                || !_sourceColumns.TryGetValue(source.Field, out var column))
            {
                AddError(
                    $"{path}.field",
                    "UnknownSourceField",
                    $"Source field '{source.Field}' does not exist.");
                return null;
            }

            _sourceDependencies.Add(column.Name);
            return new InferredExpressionType(
                ToExpressionType(column.DataType),
                !column.IsRequired);
        }

        private InferredExpressionType? CompileMetadata(
            MetadataExpression metadata,
            string path)
        {
            if (!string.IsNullOrWhiteSpace(metadata.Name)
                && MetadataCatalog.TryGetValue(metadata.Name, out var type))
            {
                return type;
            }

            AddError(
                $"{path}.name",
                "UnknownMetadata",
                $"Metadata value '{metadata.Name}' is not supported.");
            return null;
        }

        private InferredExpressionType? CompileLiteral(
            LiteralExpression literal,
            string path)
        {
            if (!IsValidLiteral(literal.Type, literal.Value))
            {
                AddError(
                    $"{path}.value",
                    "InvalidLiteral",
                    $"The literal value is not valid for type '{literal.Type}'.");
                return null;
            }

            return new InferredExpressionType(
                literal.Type,
                literal.Value.ValueKind is JsonValueKind.Null
                    or JsonValueKind.Undefined);
        }

        private InferredExpressionType? CompileCoalesce(
            CoalesceExpression expression,
            string path,
            int depth)
        {
            if (!ValidateCollection(
                expression.Values,
                $"{path}.values",
                "CoalesceValuesRequired"))
            {
                return null;
            }

            var types = expression.Values
                .Select((value, index) => value is null
                    ? MissingValue($"{path}.values[{index}]")
                    : CompileValue(
                        value,
                        $"{path}.values[{index}]",
                        depth + 1))
                .ToArray();
            return UnifyTypes(types, $"{path}.values", "ExpressionTypeMismatch");
        }

        private InferredExpressionType? CompileDefaultIfBlank(
            DefaultIfBlankExpression expression,
            string path,
            int depth)
        {
            var valueType = expression.Value is null
                ? MissingValue($"{path}.value")
                : CompileValue(expression.Value, $"{path}.value", depth + 1);
            var fallbackType = expression.Fallback is null
                ? MissingValue($"{path}.fallback")
                : CompileValue(
                    expression.Fallback,
                    $"{path}.fallback",
                    depth + 1);

            if (valueType is not null
                && valueType.Value.Type != ExpressionValueType.Text)
            {
                AddError(
                    $"{path}.value",
                    "TextValueRequired",
                    "defaultIfBlank requires a Text value.");
                return null;
            }

            if (valueType is null || fallbackType is null)
            {
                return null;
            }

            if (valueType.Value.Type != fallbackType.Value.Type)
            {
                AddError(
                    path,
                    "ExpressionTypeMismatch",
                    "The value and fallback expressions must have the same type.");
                return null;
            }

            return new InferredExpressionType(
                valueType.Value.Type,
                fallbackType.Value.IsNullable);
        }

        private InferredExpressionType? CompileTrim(
            TrimExpression expression,
            string path,
            int depth)
        {
            var valueType = expression.Value is null
                ? MissingValue($"{path}.value")
                : CompileValue(expression.Value, $"{path}.value", depth + 1);
            if (valueType is null)
            {
                return null;
            }

            if (valueType.Value.Type != ExpressionValueType.Text)
            {
                AddError(
                    $"{path}.value",
                    "TextValueRequired",
                    "trim requires a Text value.");
                return null;
            }

            return valueType;
        }

        private InferredExpressionType? CompileCase(
            CaseExpression expression,
            string path,
            int depth)
        {
            if (!ValidateCollection(
                expression.Cases,
                $"{path}.cases",
                "CaseArmsRequired"))
            {
                return null;
            }

            var resultTypes = new List<InferredExpressionType?>();
            for (var index = 0; index < expression.Cases.Count; index++)
            {
                var arm = expression.Cases[index];
                var armPath = $"{path}.cases[{index}]";
                if (arm is null)
                {
                    AddError(
                        armPath,
                        "CaseArmRequired",
                        "A CASE arm is required.");
                    continue;
                }

                if (arm.When is null)
                {
                    AddError(
                        $"{armPath}.when",
                        "BooleanExpressionRequired",
                        "A CASE condition is required.");
                }
                else
                {
                    CompileBoolean(arm.When, $"{armPath}.when", depth + 1);
                }

                resultTypes.Add(arm.Then is null
                    ? MissingValue($"{armPath}.then")
                    : CompileValue(arm.Then, $"{armPath}.then", depth + 1));
            }

            resultTypes.Add(expression.Else is null
                ? MissingValue($"{path}.else")
                : CompileValue(expression.Else, $"{path}.else", depth + 1));
            return UnifyCaseTypes(
                resultTypes,
                path,
                "CaseResultTypeMismatch");
        }

        private InferredExpressionType? CompileSubstring(
            SubstringExpression expression,
            string path,
            int depth)
        {
            var valueType = expression.Value is null
                ? MissingValue($"{path}.value")
                : CompileValue(expression.Value, $"{path}.value", depth + 1);
            if (valueType is not null
                && valueType.Value.Type != ExpressionValueType.Text)
            {
                AddError(
                    $"{path}.value",
                    "TextValueRequired",
                    "substring requires a Text value.");
                valueType = null;
            }

            if (expression.Start < 1)
            {
                AddError(
                    $"{path}.start",
                    "InvalidSubstringStart",
                    "substring start is one-based and must be at least 1.");
            }

            if (expression.Length < 0)
            {
                AddError(
                    $"{path}.length",
                    "InvalidSubstringLength",
                    "substring length must not be negative.");
            }

            return valueType;
        }

        private InferredExpressionType? CompileConvert(
            ConvertExpression expression,
            string path,
            int depth)
        {
            var valueType = expression.Value is null
                ? MissingValue($"{path}.value")
                : CompileValue(expression.Value, $"{path}.value", depth + 1);
            if (valueType is null)
            {
                return null;
            }

            if (valueType.Value.Type != ExpressionValueType.Text)
            {
                AddError(
                    $"{path}.value",
                    "ConvertRequiresText",
                    "convert requires a Text input.");
                return null;
            }

            if (expression.Type == ExpressionValueType.Text)
            {
                AddError(
                    $"{path}.type",
                    "ConvertTargetNotSupported",
                    "Converting Text to Text is not supported.");
                return null;
            }

            if (expression.InputFormat is not null)
            {
                if (expression.Type is not ExpressionValueType.Date
                    and not ExpressionValueType.DateTime)
                {
                    AddError(
                        $"{path}.inputFormat",
                        "InputFormatNotSupported",
                        "inputFormat is supported only for Date and DateTime conversion.");
                }
                else
                {
                    ValidateKnownInputFormat(
                        expression.InputFormat,
                        expression.Type,
                        $"{path}.inputFormat");
                }
            }

            return new InferredExpressionType(
                expression.Type,
                valueType.Value.IsNullable);
        }

        private InferredExpressionType? CompileCall(
            CallExpression expression,
            string path,
            int depth)
        {
            if (!string.Equals(
                expression.Function,
                TransformationCapabilities.FormatRegNoV1,
                StringComparison.Ordinal))
            {
                AddError(
                    $"{path}.function",
                    "UnknownFunction",
                    $"Function '{expression.Function}' is not supported.");
                CompileCallArguments(expression, path, depth);
                return null;
            }

            if (expression.Arguments is null || expression.Arguments.Count != 1)
            {
                AddError(
                    $"{path}.arguments",
                    "FunctionArgumentCountMismatch",
                    "formatRegNo@1 requires exactly one argument.");
                CompileCallArguments(expression, path, depth);
                return null;
            }

            var argumentType = expression.Arguments[0] is null
                ? MissingValue($"{path}.arguments[0]")
                : CompileValue(
                    expression.Arguments[0],
                    $"{path}.arguments[0]",
                    depth + 1);
            if (argumentType is null)
            {
                return null;
            }

            if (argumentType.Value.Type != ExpressionValueType.Text)
            {
                AddError(
                    $"{path}.arguments[0]",
                    "FunctionArgumentTypeMismatch",
                    "formatRegNo@1 requires a Text argument.");
                return null;
            }

            return new InferredExpressionType(
                ExpressionValueType.Text,
                argumentType.Value.IsNullable);
        }

        private void CompileCallArguments(
            CallExpression expression,
            string path,
            int depth)
        {
            if (expression.Arguments is null)
            {
                return;
            }

            for (var index = 0; index < expression.Arguments.Count; index++)
            {
                if (expression.Arguments[index] is not null)
                {
                    CompileValue(
                        expression.Arguments[index],
                        $"{path}.arguments[{index}]",
                        depth + 1);
                }
            }
        }

        private bool CompileBoolean(
            BooleanExpression expression,
            string path,
            int depth)
        {
            if (!EnterExpression(path, depth))
            {
                return false;
            }

            return expression switch
            {
                EqualExpression equal => CompileComparison(
                    equal.Left,
                    equal.Right,
                    path,
                    depth,
                    false),
                NotEqualExpression notEqual => CompileComparison(
                    notEqual.Left,
                    notEqual.Right,
                    path,
                    depth,
                    false),
                GreaterThanExpression greaterThan => CompileComparison(
                    greaterThan.Left,
                    greaterThan.Right,
                    path,
                    depth,
                    true),
                GreaterThanOrEqualExpression greaterThanOrEqual =>
                    CompileComparison(
                        greaterThanOrEqual.Left,
                        greaterThanOrEqual.Right,
                        path,
                        depth,
                        true),
                LessThanExpression lessThan => CompileComparison(
                    lessThan.Left,
                    lessThan.Right,
                    path,
                    depth,
                    true),
                LessThanOrEqualExpression lessThanOrEqual => CompileComparison(
                    lessThanOrEqual.Left,
                    lessThanOrEqual.Right,
                    path,
                    depth,
                    true),
                IsNullExpression isNull => CompileUnaryValue(
                    isNull.Value,
                    path,
                    depth),
                IsBlankExpression isBlank => CompileTextPredicate(
                    isBlank.Value,
                    path,
                    depth),
                NotBlankExpression notBlank => CompileTextPredicate(
                    notBlank.Value,
                    path,
                    depth),
                LikeExpression like => CompileLike(like, path, depth),
                InExpression inExpression => CompileIn(
                    inExpression.Value,
                    inExpression.Values,
                    path,
                    depth),
                NotInExpression notInExpression => CompileIn(
                    notInExpression.Value,
                    notInExpression.Values,
                    path,
                    depth),
                AndExpression andExpression => CompileBooleanCollection(
                    andExpression.Operands,
                    path,
                    depth),
                OrExpression orExpression => CompileBooleanCollection(
                    orExpression.Operands,
                    path,
                    depth),
                NotExpression notExpression => CompileNot(
                    notExpression,
                    path,
                    depth),
                ExistsExpression exists => CompileLookup(
                    exists.Lookup,
                    exists.Arguments,
                    path,
                    depth),
                NotExistsExpression notExists => CompileLookup(
                    notExists.Lookup,
                    notExists.Arguments,
                    path,
                    depth),
                _ => UnknownBooleanExpression(path)
            };
        }

        private bool CompileComparison(
            ValueExpression? left,
            ValueExpression? right,
            string path,
            int depth,
            bool requiresOrderedType)
        {
            var leftType = left is null
                ? MissingValue($"{path}.left")
                : CompileValue(left, $"{path}.left", depth + 1);
            var rightType = right is null
                ? MissingValue($"{path}.right")
                : CompileValue(right, $"{path}.right", depth + 1);
            if (leftType is null || rightType is null)
            {
                return false;
            }

            if (leftType.Value.Type != rightType.Value.Type)
            {
                AddError(
                    path,
                    "ComparisonTypeMismatch",
                    "Comparison operands must have the same type.");
                return false;
            }

            if (requiresOrderedType
                && leftType.Value.Type is ExpressionValueType.Boolean
                    or ExpressionValueType.Guid)
            {
                AddError(
                    path,
                    "OrderedComparisonNotSupported",
                    $"Ordered comparison is not supported for '{leftType.Value.Type}'.");
                return false;
            }

            return true;
        }

        private bool CompileUnaryValue(
            ValueExpression? value,
            string path,
            int depth)
        {
            return value is null
                ? MissingValue($"{path}.value") is not null
                : CompileValue(value, $"{path}.value", depth + 1) is not null;
        }

        private bool CompileTextPredicate(
            ValueExpression? value,
            string path,
            int depth)
        {
            var valueType = value is null
                ? MissingValue($"{path}.value")
                : CompileValue(value, $"{path}.value", depth + 1);
            if (valueType is null)
            {
                return false;
            }

            if (valueType.Value.Type == ExpressionValueType.Text)
            {
                return true;
            }

            AddError(
                $"{path}.value",
                "TextValueRequired",
                "The predicate requires a Text value.");
            return false;
        }

        private bool CompileLike(
            LikeExpression expression,
            string path,
            int depth)
        {
            var valueValid = CompileTextValue(
                expression.Value,
                $"{path}.value",
                depth + 1);
            var patternValid = CompileTextValue(
                expression.Pattern,
                $"{path}.pattern",
                depth + 1);
            return valueValid && patternValid;
        }

        private bool CompileTextValue(
            ValueExpression? value,
            string path,
            int depth)
        {
            var valueType = value is null
                ? MissingValue(path)
                : CompileValue(value, path, depth);
            if (valueType is null)
            {
                return false;
            }

            if (valueType.Value.Type == ExpressionValueType.Text)
            {
                return true;
            }

            AddError(
                path,
                "TextValueRequired",
                "A Text value is required.");
            return false;
        }

        private bool CompileIn(
            ValueExpression? value,
            IReadOnlyList<ValueExpression>? values,
            string path,
            int depth)
        {
            var valueType = value is null
                ? MissingValue($"{path}.value")
                : CompileValue(value, $"{path}.value", depth + 1);
            if (!ValidateCollection(
                values,
                $"{path}.values",
                "InValuesRequired"))
            {
                return false;
            }

            var valid = valueType is not null;
            for (var index = 0; index < values!.Count; index++)
            {
                var candidateType = values[index] is null
                    ? MissingValue($"{path}.values[{index}]")
                    : CompileValue(
                        values[index],
                        $"{path}.values[{index}]",
                        depth + 1);
                if (candidateType is null || valueType is null)
                {
                    valid = false;
                }
                else if (candidateType.Value.Type != valueType.Value.Type)
                {
                    AddError(
                        $"{path}.values[{index}]",
                        "InValueTypeMismatch",
                        "IN values must have the same type as the tested value.");
                    valid = false;
                }
            }

            return valid;
        }

        private bool CompileBooleanCollection(
            IReadOnlyList<BooleanExpression>? operands,
            string path,
            int depth)
        {
            if (!ValidateCollection(
                operands,
                $"{path}.operands",
                "BooleanOperandsRequired"))
            {
                return false;
            }

            var valid = true;
            for (var index = 0; index < operands!.Count; index++)
            {
                if (operands[index] is null)
                {
                    AddError(
                        $"{path}.operands[{index}]",
                        "BooleanExpressionRequired",
                        "A boolean expression is required.");
                    valid = false;
                }
                else
                {
                    valid = CompileBoolean(
                        operands[index],
                        $"{path}.operands[{index}]",
                        depth + 1) && valid;
                }
            }

            return valid;
        }

        private bool CompileNot(
            NotExpression expression,
            string path,
            int depth)
        {
            if (expression.Value is null)
            {
                AddError(
                    $"{path}.value",
                    "BooleanExpressionRequired",
                    "A boolean expression is required.");
                return false;
            }

            return CompileBoolean(expression.Value, $"{path}.value", depth + 1);
        }

        private bool CompileLookup(
            string lookup,
            IReadOnlyDictionary<string, ValueExpression>? arguments,
            string path,
            int depth)
        {
            if (!string.Equals(
                lookup,
                TransformationCapabilities.MtiOriginalPolicyExistsV1,
                StringComparison.Ordinal))
            {
                AddError(
                    $"{path}.lookup",
                    "UnknownLookup",
                    $"Lookup '{lookup}' is not supported.");
                CompileLookupArguments(arguments, path, depth);
                return false;
            }

            if (arguments is null)
            {
                AddError(
                    $"{path}.arguments",
                    "LookupArgumentsRequired",
                    "Lookup arguments are required.");
                return false;
            }

            var valid = true;
            foreach (var argumentName in MtiLookupArguments)
            {
                if (!arguments.TryGetValue(argumentName, out var argument))
                {
                    AddError(
                        $"{path}.arguments.{argumentName}",
                        "LookupArgumentMissing",
                        $"Lookup argument '{argumentName}' is required.");
                    valid = false;
                    continue;
                }

                var argumentType = argument is null
                    ? MissingValue($"{path}.arguments.{argumentName}")
                    : CompileValue(
                        argument,
                        $"{path}.arguments.{argumentName}",
                        depth + 1);
                if (argumentType is null)
                {
                    valid = false;
                }
                else if (argumentType.Value.Type != ExpressionValueType.Text)
                {
                    AddError(
                        $"{path}.arguments.{argumentName}",
                        "LookupArgumentTypeMismatch",
                        $"Lookup argument '{argumentName}' must be Text.");
                    valid = false;
                }
            }

            foreach (var argumentName in arguments.Keys)
            {
                if (!MtiLookupArguments.Contains(
                    argumentName,
                    StringComparer.Ordinal))
                {
                    AddError(
                        $"{path}.arguments.{argumentName}",
                        "UnknownLookupArgument",
                        $"Lookup argument '{argumentName}' is not supported.");
                    valid = false;
                }
            }

            if (valid)
            {
                _lookupDependencies.Add(new CompiledLookupDependency(
                    lookup,
                    path,
                    arguments));
            }

            return valid;
        }

        private void CompileLookupArguments(
            IReadOnlyDictionary<string, ValueExpression>? arguments,
            string path,
            int depth)
        {
            if (arguments is null)
            {
                return;
            }

            foreach (var argument in arguments)
            {
                if (argument.Value is not null)
                {
                    CompileValue(
                        argument.Value,
                        $"{path}.arguments.{argument.Key}",
                        depth + 1);
                }
            }
        }

        private bool ValidateCollection<T>(
            IReadOnlyCollection<T>? values,
            string path,
            string emptyCode)
        {
            if (values is null || values.Count == 0)
            {
                AddError(path, emptyCode, "At least one item is required.");
                return false;
            }

            if (values.Count > MaxCollectionItems)
            {
                AddError(
                    path,
                    "CollectionLimitExceeded",
                    $"A collection may contain at most {MaxCollectionItems} items.");
                return false;
            }

            return true;
        }

        private InferredExpressionType? UnifyTypes(
            IEnumerable<InferredExpressionType?> types,
            string path,
            string mismatchCode)
        {
            var validTypes = types.Where(type => type is not null)
                .Select(type => type!.Value)
                .ToArray();
            if (validTypes.Length == 0)
            {
                return null;
            }

            var resultType = validTypes[0].Type;
            if (validTypes.Any(type => type.Type != resultType))
            {
                AddError(
                    path,
                    mismatchCode,
                    "All result expressions must have the same type.");
                return null;
            }

            return new InferredExpressionType(
                resultType,
                validTypes.All(type => type.IsNullable));
        }

        private InferredExpressionType? UnifyCaseTypes(
            IEnumerable<InferredExpressionType?> types,
            string path,
            string mismatchCode)
        {
            var validTypes = types.Where(type => type is not null)
                .Select(type => type!.Value)
                .ToArray();
            if (validTypes.Length == 0)
            {
                return null;
            }

            var resultType = validTypes[0].Type;
            if (validTypes.Any(type => type.Type != resultType))
            {
                AddError(
                    path,
                    mismatchCode,
                    "All CASE result expressions must have the same type.");
                return null;
            }

            return new InferredExpressionType(
                resultType,
                validTypes.Any(type => type.IsNullable));
        }

        private bool EnterExpression(string path, int depth)
        {
            if (depth >= MaxExpressionDepth)
            {
                AddError(
                    path,
                    "ExpressionDepthExceeded",
                    $"Expression depth must be less than {MaxExpressionDepth}.");
                return false;
            }

            _expressionNodes++;
            if (_expressionNodes <= MaxExpressionNodes)
            {
                return true;
            }

            if (!_nodeLimitReported)
            {
                AddError(
                    path,
                    "ExpressionNodeLimitExceeded",
                    $"A program may contain at most {MaxExpressionNodes} expression nodes.");
                _nodeLimitReported = true;
            }

            return false;
        }

        private void ValidateKnownInputFormat(
            string inputFormat,
            ExpressionValueType targetType,
            string path)
        {
            var supported = targetType == ExpressionValueType.Date
                ? TransformationCapabilities.DateInputFormats
                : TransformationCapabilities.DateTimeInputFormats;
            if (!supported.Contains(inputFormat, StringComparer.Ordinal))
            {
                AddError(
                    path,
                    "UnknownInputFormat",
                    $"Input format '{inputFormat}' is not supported for '{targetType}'.");
            }
        }

        private static bool IsValidLiteral(
            ExpressionValueType type,
            JsonElement value)
        {
            if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                return true;
            }

            return type switch
            {
                ExpressionValueType.Text => value.ValueKind == JsonValueKind.String,
                ExpressionValueType.Decimal => value.ValueKind == JsonValueKind.Number
                    && value.TryGetDecimal(out _),
                ExpressionValueType.Integer => value.ValueKind == JsonValueKind.Number
                    && value.TryGetInt32(out _),
                ExpressionValueType.Date => value.ValueKind == JsonValueKind.String
                    && DateOnly.TryParseExact(
                        value.GetString(),
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out _),
                ExpressionValueType.DateTime => value.ValueKind == JsonValueKind.String
                    && DateTimeOffset.TryParse(
                        value.GetString(),
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind,
                        out _),
                ExpressionValueType.Boolean => value.ValueKind is JsonValueKind.True
                    or JsonValueKind.False,
                ExpressionValueType.Guid => value.ValueKind == JsonValueKind.String
                    && Guid.TryParse(value.GetString(), out _),
                _ => false
            };
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
                _ => throw new ArgumentOutOfRangeException(
                    nameof(dataType),
                    dataType,
                    "Unsupported column type.")
            };
        }

        private InferredExpressionType? MissingValue(string path)
        {
            AddError(
                path,
                "ExpressionRequired",
                "A value expression is required.");
            return null;
        }

        private InferredExpressionType? UnknownExpression(string path)
        {
            AddError(
                path,
                "UnknownExpression",
                "The value expression kind is not supported.");
            return null;
        }

        private bool UnknownBooleanExpression(string path)
        {
            AddError(
                path,
                "UnknownBooleanExpression",
                "The boolean expression kind is not supported.");
            return false;
        }

        private void AddError(string path, string code, string message)
        {
            _diagnostics.Add(
                new TransformationDiagnostic(
                    path,
                    code,
                    message,
                    TransformationDiagnosticSeverity.Error));
        }

        private TransformationCompilationResult Failure()
        {
            return new TransformationCompilationResult(null, _diagnostics);
        }
    }
}
