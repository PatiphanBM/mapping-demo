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

        private static readonly string[] MtiLoanLookupArguments =
            ["systemId", "loanNumber"];

        private readonly TransformationProgram _program;
        private readonly TransformationSchema _schema;
        private readonly List<TransformationDiagnostic> _diagnostics = [];
        private readonly HashSet<string> _sourceDependencies =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly List<CompiledLookupDependency> _lookupDependencies = [];
        private readonly Dictionary<string, ColumnDefinition> _sourceColumns;
        private readonly Dictionary<string, TargetNode> _targetNodes;
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
            _targetNodes = schema.TargetSchema.Root.Properties.ToDictionary(
                node => node.Name,
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
            if (languageVersion is not 1 and not 2)
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
                return new CompiledTransformationOutput(output, [], []);
            }

            if (output.Fields.Count > MaxFieldsPerOutput)
            {
                AddError(
                    $"{outputPath}.fields",
                    "FieldLimitExceeded",
                    $"An output may contain at most {MaxFieldsPerOutput} fields.");
            }

            var languageVersion = _program.LanguageVersion ?? DefaultLanguageVersion;
            var assignedTargets = new HashSet<string>(StringComparer.Ordinal);
            var assignedPaths = new List<ResolvedTargetPath>();
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

                TargetNode? targetNode = null;
                ResolvedTargetPath? resolvedPath = null;
                var target = languageVersion == 1
                    ? assignment.NormalizedColumn
                    : assignment.TargetPath;
                var targetProperty = languageVersion == 1
                    ? "normalizedColumn"
                    : "targetPath";
                if (string.IsNullOrWhiteSpace(target))
                {
                    AddError(
                        $"{fieldPath}.{targetProperty}",
                        "TargetRequired",
                        languageVersion == 1
                            ? "A normalized target column is required."
                            : "A contract target path is required.");
                }
                else if (languageVersion == 1)
                {
                    if (!assignedTargets.Add(target!))
                    {
                        AddError($"{fieldPath}.normalizedColumn", "DuplicateTarget", $"Target column '{target}' is assigned more than once.");
                    }
                    else if (!_targetNodes.TryGetValue(target!, out targetNode))
                    {
                        AddError($"{fieldPath}.normalizedColumn", "UnknownTargetColumn", $"Target column '{target}' does not exist.");
                    }
                    else
                    {
                        resolvedPath = new ResolvedTargetPath(
                            target!,
                            targetNode,
                            [new PropertyPathToken(targetNode.Name)]);
                    }
                }
                else
                {
                    resolvedPath = TargetPath.Resolve(_schema.TargetSchema, target!);
                    if (resolvedPath is null)
                    {
                        AddError($"{fieldPath}.targetPath", "UnknownTargetPath", $"Target path '{target}' does not exist.");
                    }
                    else if (resolvedPath.Node.Kind != TargetNodeKind.Scalar)
                    {
                        AddError($"{fieldPath}.targetPath", "TargetContainerNotAssignable", $"Target path '{target}' is a container and cannot be assigned.");
                        resolvedPath = null;
                    }
                    else if (assignedPaths.Any(existing => TargetPath.Conflicts(existing.Tokens, resolvedPath.Tokens)))
                    {
                        AddError($"{fieldPath}.targetPath", "ConflictingTargetPath", $"Target path '{target}' duplicates or conflicts with another assignment.");
                        resolvedPath = null;
                    }
                    else
                    {
                        targetNode = resolvedPath.Node;
                        assignedPaths.Add(resolvedPath);
                        assignedTargets.Add(target!);
                    }
                }

                var valueType = assignment.Value is null
                    ? MissingValue($"{fieldPath}.value")
                    : CompileValue(assignment.Value, $"{fieldPath}.value", 0);

                if (targetNode is not null && resolvedPath is not null && valueType is not null)
                {
                    var coercion = CompileAssignment(
                        assignment,
                        targetNode,
                        valueType.Value,
                        fieldPath);
                    if (coercion is not null)
                    {
                        compiledFields.Add(
                            new CompiledFieldAssignment(
                                assignment,
                                targetNode,
                                resolvedPath,
                                valueType.Value,
                                coercion.Value));
                    }
                }
            }

            if (languageVersion == 1)
            {
                foreach (var requiredTarget in _targetNodes.Values
                    .Where(node => node.IsRequired))
                {
                    if (!assignedTargets.Contains(requiredTarget.Name))
                    {
                        AddError(
                            $"{outputPath}.fields",
                            "RequiredTargetMissing",
                            $"Required target column '{requiredTarget.Name}' is not assigned.");
                    }
                }
            }
            else
            {
                ValidateContiguousFixedIndices(outputPath, assignedPaths);
            }

            var compiledGroups = languageVersion == 2
                ? CompileArrayGroups(output, outputPath, assignedPaths)
                : [];
            if (languageVersion == 2)
            {
                ValidateRequiredContractTargets(
                    outputPath,
                    assignedTargets,
                    compiledGroups);
            }
            if (languageVersion == 1 && (output.ArrayGroups?.Count ?? 0) > 0)
            {
                AddError($"{outputPath}.arrayGroups", "ArrayGroupsRequireV2", "Array groups require language version 2.");
            }

            return new CompiledTransformationOutput(output, compiledFields, compiledGroups);
        }

        private void ValidateContiguousFixedIndices(
            string outputPath,
            IReadOnlyList<ResolvedTargetPath> paths)
        {
            var arrays = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
            foreach (var path in paths)
            {
                var prefix = new List<string>();
                foreach (var token in path.Tokens)
                {
                    if (token is PropertyPathToken property)
                    {
                        prefix.Add(property.Name);
                    }
                    else if (token is IndexPathToken index)
                    {
                        var key = string.Join('.', prefix);
                        if (!arrays.TryGetValue(key, out var indices))
                        {
                            indices = [];
                            arrays[key] = indices;
                        }
                        indices.Add(index.Index);
                        prefix[^1] += $"[{index.Index}]";
                    }
                }
            }

            foreach (var (path, indices) in arrays)
            {
                var maximum = indices.Max();
                if (Enumerable.Range(0, maximum + 1).Any(index => !indices.Contains(index)))
                {
                    AddError(
                        $"{outputPath}.fields",
                        "ArrayIndexGap",
                        $"Fixed indices for array '{path}' must be contiguous from 0.");
                }
            }
        }

        private void ValidateRequiredContractTargets(
            string outputPath,
            IReadOnlySet<string> assignedTargets,
            IReadOnlyList<CompiledArrayGroup> groups)
        {
            var systemPaths = _schema.SystemBoundPaths ??
                new HashSet<string>(StringComparer.Ordinal);
            Visit(_schema.TargetSchema.Root, parentRequired: true, underArray: false);

            void Visit(TargetNode node, bool parentRequired, bool underArray)
            {
                var required = parentRequired && node.IsRequired;
                var path = string.Join('.', node.PathTokens);
                if (node.Kind == TargetNodeKind.Scalar && required && !underArray &&
                    !assignedTargets.Contains(path) && !systemPaths.Contains(path))
                {
                    AddError(
                        $"{outputPath}.fields",
                        "RequiredTargetMissing",
                        $"Required target path '{path}' is not assigned.");
                }
                if (node.Kind == TargetNodeKind.Array && required &&
                    !groups.Any(group => group.Definition.TargetPath == path) &&
                    !assignedTargets.Any(target => target.StartsWith(path + "[", StringComparison.Ordinal)))
                {
                    AddError(
                        $"{outputPath}.arrayGroups",
                        "RequiredTargetMissing",
                        $"Required array path '{path}' is not assigned.");
                }
                foreach (var child in node.Properties)
                {
                    Visit(child, required, underArray);
                }
                if (node.ItemSchema is not null)
                {
                    Visit(node.ItemSchema, required, underArray: true);
                }
            }
        }

        private IReadOnlyList<CompiledArrayGroup> CompileArrayGroups(
            TransformationOutput output,
            string outputPath,
            IReadOnlyList<ResolvedTargetPath> directPaths)
        {
            var result = new List<CompiledArrayGroup>();
            var groups = output.ArrayGroups ?? [];
            var groupTargets = new HashSet<string>(StringComparer.Ordinal);
            for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                var group = groups[groupIndex];
                var groupPath = $"{outputPath}.arrayGroups[{groupIndex}]";
                var arrayPath = TargetPath.Resolve(_schema.TargetSchema, group.TargetPath);
                if (arrayPath is null)
                {
                    AddError($"{groupPath}.targetPath", "UnknownArrayTargetPath", $"Array target path '{group.TargetPath}' does not exist.");
                    continue;
                }
                if (arrayPath.Node.Kind != TargetNodeKind.Array || arrayPath.Node.ItemSchema is null || arrayPath.Node.ItemSchema.Kind != TargetNodeKind.Object)
                {
                    AddError($"{groupPath}.targetPath", "ArrayObjectTargetRequired", "Array group target must be an array of objects.");
                    continue;
                }
                if (!groupTargets.Add(group.TargetPath))
                {
                    AddError($"{groupPath}.targetPath", "DuplicateArrayGroup", $"Array group '{group.TargetPath}' is duplicated.");
                }
                if (directPaths.Any(path => TargetPath.Conflicts(path.Tokens, arrayPath.Tokens)))
                {
                    AddError($"{groupPath}.targetPath", "ArrayGroupDirectConflict", "An array cannot be written by both direct fields and an array group.");
                }

                var sequence = TargetPath.Resolve(
                    _schema.TargetSchema,
                    group.SequencePath,
                    relativeToArrayItem: true,
                    arrayItem: arrayPath.Node.ItemSchema);
                if (sequence is null || sequence.Node.Kind != TargetNodeKind.Scalar || sequence.Node.ScalarType != TargetScalarType.Integer)
                {
                    AddError($"{groupPath}.sequencePath", "IntegerSequencePathRequired", "Sequence path must identify an integer item leaf.");
                    continue;
                }

                var slots = new List<CompiledArraySourceSlot>();
                var sourceSlots = new HashSet<int>();
                for (var slotIndex = 0; slotIndex < group.SourceSlots.Count; slotIndex++)
                {
                    var slot = group.SourceSlots[slotIndex];
                    var slotPath = $"{groupPath}.sourceSlots[{slotIndex}]";
                    if (slot.SourceSlot <= 0 || !sourceSlots.Add(slot.SourceSlot))
                    {
                        AddError($"{slotPath}.sourceSlot", "InvalidSourceSlot", "Source slot must be a unique positive integer.");
                    }
                    var fields = new List<CompiledArrayItemField>();
                    var itemPaths = new List<ResolvedTargetPath>();
                    for (var itemIndex = 0; itemIndex < slot.Fields.Count; itemIndex++)
                    {
                        var item = slot.Fields[itemIndex];
                        var itemPath = $"{slotPath}.fields[{itemIndex}]";
                        var resolved = TargetPath.Resolve(
                            _schema.TargetSchema,
                            item.TargetPath,
                            relativeToArrayItem: true,
                            arrayItem: arrayPath.Node.ItemSchema);
                        if (resolved is null)
                        {
                            AddError($"{itemPath}.targetPath", "UnknownArrayItemPath", $"Array item path '{item.TargetPath}' does not exist.");
                            continue;
                        }
                        if (resolved.Node.Kind != TargetNodeKind.Scalar)
                        {
                            AddError($"{itemPath}.targetPath", "TargetContainerNotAssignable", "Array item path must identify a scalar leaf.");
                            continue;
                        }
                        if (TargetPath.Conflicts(resolved.Tokens, sequence.Tokens) || itemPaths.Any(existing => TargetPath.Conflicts(existing.Tokens, resolved.Tokens)))
                        {
                            AddError($"{itemPath}.targetPath", "ConflictingTargetPath", "Array item path duplicates or conflicts with another item field or sequence path.");
                            continue;
                        }
                        itemPaths.Add(resolved);
                        var valueType = item.Value is null
                            ? MissingValue($"{itemPath}.value")
                            : CompileValue(item.Value, $"{itemPath}.value", 0);
                        if (valueType is null) continue;
                        var coercion = CompileAssignment(
                            item.InputFormat,
                            resolved.Node,
                            valueType.Value,
                            itemPath);
                        if (coercion is not null)
                        {
                            fields.Add(new CompiledArrayItemField(
                                item,
                                resolved.Node,
                                resolved,
                                valueType.Value,
                                coercion.Value));
                        }
                    }
                    slots.Add(new CompiledArraySourceSlot(slot, fields));
                }
                result.Add(new CompiledArrayGroup(group, arrayPath, sequence, slots));
            }
            return result;
        }

        private TargetAssignmentCoercion? CompileAssignment(
            FieldAssignment assignment,
            TargetNode targetNode,
            InferredExpressionType valueType,
            string fieldPath)
        {
            return CompileAssignment(
                assignment.InputFormat,
                targetNode,
                valueType,
                fieldPath);
        }

        private TargetAssignmentCoercion? CompileAssignment(
            string? inputFormat,
            TargetNode targetNode,
            InferredExpressionType valueType,
            string fieldPath)
        {
            var targetType = ToExpressionType(
                targetNode.ScalarType
                ?? throw new InvalidOperationException(
                    $"Target '{targetNode.Name}' must be scalar."));
            ValidateAssignmentInputFormat(
                inputFormat,
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
                and not ExpressionValueType.Time
                and not ExpressionValueType.DateTime)
            {
                AddError(
                    path,
                    "InputFormatNotSupported",
                    "inputFormat is supported only for Date, Time and DateTime targets.");
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
                AddExpression add => CompileAdd(add, path, depth),
                ConcatExpression concat => CompileConcat(concat, path, depth),
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

        private InferredExpressionType? CompileAdd(
            AddExpression expression,
            string path,
            int depth)
        {
            if (!ValidateOperandCollection(
                expression.Values,
                $"{path}.values",
                "AddOperandsRequired",
                "add"))
            {
                return null;
            }

            var types = CompileValues(expression.Values, path, depth);
            var validTypes = types.Where(type => type is not null)
                .Select(type => type!.Value)
                .ToArray();
            var valid = validTypes.Length == expression.Values.Count;
            for (var index = 0; index < types.Length; index++)
            {
                if (types[index] is not { } type)
                {
                    valid = false;
                    continue;
                }

                if (type.Type is ExpressionValueType.Integer
                    or ExpressionValueType.Decimal)
                {
                    continue;
                }

                AddError(
                    $"{path}.values[{index}]",
                    "NumericValueRequired",
                    "add requires Integer or Decimal values.");
                valid = false;
            }

            if (!valid)
            {
                return null;
            }

            return new InferredExpressionType(
                validTypes.Any(type =>
                    type.Type == ExpressionValueType.Decimal)
                    ? ExpressionValueType.Decimal
                    : ExpressionValueType.Integer,
                validTypes.Any(type => type.IsNullable));
        }

        private InferredExpressionType? CompileConcat(
            ConcatExpression expression,
            string path,
            int depth)
        {
            if (!ValidateOperandCollection(
                expression.Values,
                $"{path}.values",
                "ConcatOperandsRequired",
                "concat"))
            {
                return null;
            }

            var valid = true;
            if (expression.Separator is null)
            {
                AddError(
                    $"{path}.separator",
                    "ConcatSeparatorRequired",
                    "concat separator must be a string literal.");
                valid = false;
            }

            var types = CompileValues(expression.Values, path, depth);
            for (var index = 0; index < types.Length; index++)
            {
                if (types[index] is not { } type)
                {
                    valid = false;
                    continue;
                }

                if (type.Type == ExpressionValueType.Text)
                {
                    continue;
                }

                AddError(
                    $"{path}.values[{index}]",
                    "TextValueRequired",
                    "concat requires Text values.");
                valid = false;
            }

            return valid
                ? new InferredExpressionType(
                    ExpressionValueType.Text,
                    types.Any(type => type!.Value.IsNullable))
                : null;
        }

        private InferredExpressionType?[] CompileValues(
            IReadOnlyList<ValueExpression> values,
            string path,
            int depth)
        {
            return values.Select((value, index) => value is null
                    ? MissingValue($"{path}.values[{index}]")
                    : CompileValue(
                        value,
                        $"{path}.values[{index}]",
                        depth + 1))
                .ToArray();
        }

        private bool ValidateOperandCollection<T>(
            IReadOnlyCollection<T>? values,
            string path,
            string minimumCode,
            string expressionName)
        {
            if (values is null || values.Count < 2)
            {
                AddError(
                    path,
                    minimumCode,
                    $"{expressionName} requires at least two values.");
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
            var expectedArguments = lookup switch
            {
                TransformationCapabilities.MtiOriginalPolicyExistsV1 =>
                    MtiLookupArguments,
                TransformationCapabilities.MtiOriginalLoanExistsV1 =>
                    MtiLoanLookupArguments,
                _ => null
            };
            if (expectedArguments is null)
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
            foreach (var argumentName in expectedArguments)
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
                if (!expectedArguments.Contains(
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
                : targetType == ExpressionValueType.Time
                    ? TransformationCapabilities.TimeInputFormats
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
                ExpressionValueType.Time => value.ValueKind == JsonValueKind.String
                    && TimeOnly.TryParse(value.GetString(), CultureInfo.InvariantCulture, out _),
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

        private static ExpressionValueType ToExpressionType(
            TargetScalarType scalarType)
        {
            return scalarType switch
            {
                TargetScalarType.Text => ExpressionValueType.Text,
                TargetScalarType.Decimal => ExpressionValueType.Decimal,
                TargetScalarType.Integer => ExpressionValueType.Integer,
                TargetScalarType.Date => ExpressionValueType.Date,
                TargetScalarType.Time => ExpressionValueType.Time,
                TargetScalarType.DateTime => ExpressionValueType.DateTime,
                TargetScalarType.Boolean => ExpressionValueType.Boolean,
                TargetScalarType.Guid => ExpressionValueType.Guid,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(scalarType),
                    scalarType,
                    "Unsupported target scalar type.")
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
