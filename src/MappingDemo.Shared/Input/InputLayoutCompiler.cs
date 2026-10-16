using MappingDemo.Shared.Tables;

namespace MappingDemo.Shared.Input;

internal static class InputLayoutCompiler
{
    internal static InputLayoutCompilationResult Compile(
        InputLayoutDefinition layout,
        TableDefinition sourceTable)
    {
        var diagnostics = new List<InputLayoutDiagnostic>();

        if (layout is DelimitedInputLayout delimited)
        {
            ValidateDelimited(delimited, sourceTable, diagnostics);
        }
        else if (layout is FixedWidthInputLayout fixedWidth)
        {
            ValidateFixedWidth(fixedWidth, sourceTable, diagnostics);
        }

        if (diagnostics.Any(diagnostic =>
                diagnostic.Severity == InputLayoutDiagnosticSeverity.Error))
        {
            return new InputLayoutCompilationResult(null, diagnostics);
        }

        return new InputLayoutCompilationResult(
            new CompiledInputLayout(layout),
            diagnostics);
    }

    private static void ValidateFixedWidth(
        FixedWidthInputLayout layout,
        TableDefinition sourceTable,
        ICollection<InputLayoutDiagnostic> diagnostics)
    {
        if (!string.Equals(
                layout.Encoding,
                InputCapabilities.Utf8Encoding,
                StringComparison.OrdinalIgnoreCase))
        {
            AddError(
                diagnostics,
                "encoding",
                "UnknownEncoding",
                "Encoding must be 'utf-8'.");
        }

        if (!string.Equals(
                layout.PositionUnit,
                InputCapabilities.Utf16CodeUnitPosition,
                StringComparison.Ordinal))
        {
            AddError(
                diagnostics,
                "positionUnit",
                "UnknownPositionUnit",
                "Position unit must be 'utf16CodeUnit'.");
        }

        if (layout.RecordRules.Count == 0)
        {
            AddError(
                diagnostics,
                "recordRules",
                "RecordRulesRequired",
                "At least one record rule is required.");
        }

        if (layout.RecordSequence.Count == 0)
        {
            AddError(
                diagnostics,
                "recordSequence",
                "RecordSequenceRequired",
                "At least one record sequence item is required.");
        }

        if (!Enum.IsDefined(layout.UnmatchedRecordPolicy))
        {
            AddError(
                diagnostics,
                "unmatchedRecordPolicy",
                "UnknownUnmatchedRecordPolicy",
                "Unmatched record policy is not supported.");
        }

        var sourceColumns = sourceTable.Columns
            .Select(column => column.Name)
            .ToHashSet(StringComparer.Ordinal);
        var ruleKeys = new HashSet<string>(StringComparer.Ordinal);

        for (var ruleIndex = 0;
             ruleIndex < layout.RecordRules.Count;
             ruleIndex++)
        {
            var rule = layout.RecordRules[ruleIndex];
            var rulePath = $"recordRules[{ruleIndex}]";
            var mappedSourceColumns =
                new HashSet<string>(StringComparer.Ordinal);

            if (!ruleKeys.Add(rule.Key))
            {
                AddError(
                    diagnostics,
                    $"{rulePath}.key",
                    "DuplicateRecordRuleKey",
                    "Record rule keys must be unique.");
            }

            if (!Enum.IsDefined(rule.Action))
            {
                AddError(
                    diagnostics,
                    $"{rulePath}.action",
                    "UnknownRecordAction",
                    "Record action is not supported.");
            }

            if (rule.ExpectedLength <= 0)
            {
                AddError(
                    diagnostics,
                    $"{rulePath}.expectedLength",
                    "InvalidExpectedLength",
                    "Expected record length must be greater than zero.");
            }

            if (rule.Discriminator.Start <= 0)
            {
                AddError(
                    diagnostics,
                    $"{rulePath}.discriminator.start",
                    "InvalidSliceStart",
                    "Discriminator start must be greater than zero.");
            }

            if (rule.Discriminator.Length <= 0)
            {
                AddError(
                    diagnostics,
                    $"{rulePath}.discriminator.length",
                    "InvalidSliceLength",
                    "Discriminator length must be greater than zero.");
            }

            if (rule.Discriminator.Value.Length !=
                rule.Discriminator.Length)
            {
                AddError(
                    diagnostics,
                    $"{rulePath}.discriminator.value",
                    "DiscriminatorLengthMismatch",
                    "Discriminator value length must match its slice length.");
            }

            if (rule.Discriminator.Start > 0 &&
                rule.Discriminator.Length > 0 &&
                (long)rule.Discriminator.Start +
                rule.Discriminator.Length - 1 > rule.ExpectedLength)
            {
                AddError(
                    diagnostics,
                    $"{rulePath}.discriminator",
                    "DiscriminatorExceedsExpectedLength",
                    "Discriminator end cannot exceed the expected record length.");
            }

            for (var fieldIndex = 0;
                 fieldIndex < rule.Fields.Count;
                 fieldIndex++)
            {
                var field = rule.Fields[fieldIndex];
                var fieldPath = $"{rulePath}.fields[{fieldIndex}]";

                if (!sourceColumns.Contains(field.SourceColumn))
                {
                    AddError(
                        diagnostics,
                        $"{fieldPath}.sourceColumn",
                        "SourceColumnNotFound",
                        "Source column does not exist.");
                }

                if (!mappedSourceColumns.Add(field.SourceColumn))
                {
                    AddError(
                        diagnostics,
                        $"{fieldPath}.sourceColumn",
                        "DuplicateSourceColumn",
                        "Source columns can only be mapped once per record rule.");
                }

                if (field.Start <= 0)
                {
                    AddError(
                        diagnostics,
                        $"{fieldPath}.start",
                        "InvalidSliceStart",
                        "Slice start must be greater than zero.");
                }

                if (field.Length <= 0)
                {
                    AddError(
                        diagnostics,
                        $"{fieldPath}.length",
                        "InvalidSliceLength",
                        "Slice length must be greater than zero.");
                }

                if (field.Start > 0 &&
                    field.Length > 0 &&
                    (long)field.Start + field.Length - 1 >
                    rule.ExpectedLength)
                {
                    AddError(
                        diagnostics,
                        fieldPath,
                        "SliceExceedsExpectedLength",
                        "Slice end cannot exceed the expected record length.");
                }
            }

            AddFieldLayoutWarnings(
                rule,
                ruleIndex,
                diagnostics);
        }

        ValidateDiscriminatorAmbiguity(
            layout.RecordRules,
            diagnostics);
        ValidateRecordSequence(layout, diagnostics);
    }

    private static void AddFieldLayoutWarnings(
        FixedWidthRecordRule rule,
        int ruleIndex,
        ICollection<InputLayoutDiagnostic> diagnostics)
    {
        var slices = rule.Fields
            .Select((field, index) => new
            {
                Field = field,
                Index = index,
                End = (long)field.Start + field.Length - 1
            })
            .Where(slice =>
                slice.Field.Start > 0 &&
                slice.Field.Length > 0 &&
                slice.End <= rule.ExpectedLength)
            .OrderBy(slice => slice.Field.Start)
            .ThenBy(slice => slice.Index)
            .ToArray();

        if (slices.Length < 2)
        {
            return;
        }

        var coveredEnd = slices[0].End;

        for (var index = 1; index < slices.Length; index++)
        {
            var slice = slices[index];
            var path =
                $"recordRules[{ruleIndex}].fields[{slice.Index}]";

            if (slice.Field.Start <= coveredEnd)
            {
                AddWarning(
                    diagnostics,
                    path,
                    "OverlappingFieldSlice",
                    "Field slice overlaps a preceding field slice.");
            }
            else if (slice.Field.Start > coveredEnd + 1)
            {
                AddWarning(
                    diagnostics,
                    path,
                    "GappedFieldSlice",
                    "There is a gap before this field slice.");
            }

            coveredEnd = Math.Max(coveredEnd, slice.End);
        }
    }

    private static void ValidateRecordSequence(
        FixedWidthInputLayout layout,
        ICollection<InputLayoutDiagnostic> diagnostics)
    {
        var ruleKeys = layout.RecordRules
            .Select(rule => rule.Key)
            .ToHashSet(StringComparer.Ordinal);
        var referencedRuleKeys =
            new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0;
             index < layout.RecordSequence.Count;
             index++)
        {
            var item = layout.RecordSequence[index];
            var path = $"recordSequence[{index}]";

            if (!ruleKeys.Contains(item.RecordRule))
            {
                AddError(
                    diagnostics,
                    $"{path}.recordRule",
                    "RecordRuleNotFound",
                    "Record sequence must reference an existing rule.");
            }

            if (!referencedRuleKeys.Add(item.RecordRule))
            {
                AddError(
                    diagnostics,
                    $"{path}.recordRule",
                    "DuplicateRecordRuleReference",
                    "A record rule can only appear once in the sequence.");
            }

            if (item.MinOccurs < 0)
            {
                AddError(
                    diagnostics,
                    $"{path}.minOccurs",
                    "InvalidMinOccurs",
                    "Minimum occurrences cannot be negative.");
            }

            if (item.MaxOccurs < 0)
            {
                AddError(
                    diagnostics,
                    $"{path}.maxOccurs",
                    "InvalidMaxOccurs",
                    "Maximum occurrences cannot be negative.");
            }
            else if (item.MaxOccurs is not null &&
                     item.MaxOccurs < item.MinOccurs)
            {
                AddError(
                    diagnostics,
                    $"{path}.maxOccurs",
                    "MaxOccursLessThanMinOccurs",
                    "Maximum occurrences cannot be less than minimum occurrences.");
            }
        }

        for (var index = 0; index < layout.RecordRules.Count; index++)
        {
            if (referencedRuleKeys.Contains(layout.RecordRules[index].Key))
            {
                continue;
            }

            AddError(
                diagnostics,
                $"recordRules[{index}].key",
                "RecordRuleNotReferenced",
                "Every record rule must appear in the sequence.");
        }
    }

    private static void ValidateDiscriminatorAmbiguity(
        IReadOnlyList<FixedWidthRecordRule> rules,
        ICollection<InputLayoutDiagnostic> diagnostics)
    {
        for (var currentIndex = 1;
             currentIndex < rules.Count;
             currentIndex++)
        {
            var current = rules[currentIndex].Discriminator;
            if (!HasValidShape(current))
            {
                continue;
            }

            for (var previousIndex = 0;
                 previousIndex < currentIndex;
                 previousIndex++)
            {
                var previous = rules[previousIndex].Discriminator;
                if (!HasValidShape(previous) ||
                    !CanMatchSameRecord(previous, current))
                {
                    continue;
                }

                AddError(
                    diagnostics,
                    $"recordRules[{currentIndex}].discriminator",
                    "AmbiguousDiscriminator",
                    $"Discriminator can match the same record as " +
                    $"recordRules[{previousIndex}].discriminator.");
                break;
            }
        }
    }

    private static bool HasValidShape(FixedWidthDiscriminator discriminator)
    {
        return discriminator.Start > 0 &&
               discriminator.Length > 0 &&
               discriminator.Value.Length == discriminator.Length;
    }

    private static bool CanMatchSameRecord(
        FixedWidthDiscriminator first,
        FixedWidthDiscriminator second)
    {
        var overlapStart = Math.Max(first.Start, second.Start);
        var overlapEnd = Math.Min(
            first.Start + first.Length - 1,
            second.Start + second.Length - 1);

        for (var position = overlapStart;
             position <= overlapEnd;
             position++)
        {
            var firstValue = first.Value[position - first.Start];
            var secondValue = second.Value[position - second.Start];

            if (firstValue != secondValue)
            {
                return false;
            }
        }

        return true;
    }

    private static void ValidateDelimited(
        DelimitedInputLayout layout,
        TableDefinition sourceTable,
        ICollection<InputLayoutDiagnostic> diagnostics)
    {
        if (!string.Equals(
                layout.Encoding,
                InputCapabilities.Utf8Encoding,
                StringComparison.OrdinalIgnoreCase))
        {
            AddError(
                diagnostics,
                "encoding",
                "UnknownEncoding",
                "Encoding must be 'utf-8'.");
        }

        if (layout.PreambleLines < 0)
        {
            AddError(
                diagnostics,
                "preambleLines",
                "InvalidPreambleLines",
                "Preamble lines cannot be negative.");
        }

        if (string.IsNullOrEmpty(layout.Delimiter) ||
            layout.Delimiter.Contains('\r') ||
            layout.Delimiter.Contains('\n'))
        {
            AddError(
                diagnostics,
                "delimiter",
                "InvalidDelimiter",
                "Delimiter is required and cannot contain a line break.");
        }

        if (!Enum.IsDefined(layout.HeaderPolicy))
        {
            AddError(
                diagnostics,
                "headerPolicy",
                "UnknownHeaderPolicy",
                "Header policy is not supported.");
        }

        if (layout.Fields.Count == 0)
        {
            AddError(
                diagnostics,
                "fields",
                "InputFieldsRequired",
                "At least one input field is required.");
        }

        var sourceColumns = sourceTable.Columns
            .Select(column => column.Name)
            .ToHashSet(StringComparer.Ordinal);
        var inputHeaders = new HashSet<string>(StringComparer.Ordinal);
        var mappedSourceColumns = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < layout.Fields.Count; index++)
        {
            var field = layout.Fields[index];
            var path = $"fields[{index}]";

            if (!sourceColumns.Contains(field.SourceColumn))
            {
                AddError(
                    diagnostics,
                    $"{path}.sourceColumn",
                    "SourceColumnNotFound",
                    "Source column does not exist.");
            }

            if (!inputHeaders.Add(field.InputHeader))
            {
                AddError(
                    diagnostics,
                    $"{path}.inputHeader",
                    "DuplicateInputHeader",
                    "Input headers must be unique.");
            }

            if (!mappedSourceColumns.Add(field.SourceColumn))
            {
                AddError(
                    diagnostics,
                    $"{path}.sourceColumn",
                    "DuplicateSourceColumn",
                    "Source columns can only be mapped once.");
            }
        }
    }

    private static void AddError(
        ICollection<InputLayoutDiagnostic> diagnostics,
        string path,
        string code,
        string message)
    {
        diagnostics.Add(new InputLayoutDiagnostic(
            path,
            code,
            message,
            InputLayoutDiagnosticSeverity.Error));
    }

    private static void AddWarning(
        ICollection<InputLayoutDiagnostic> diagnostics,
        string path,
        string code,
        string message)
    {
        diagnostics.Add(new InputLayoutDiagnostic(
            path,
            code,
            message,
            InputLayoutDiagnosticSeverity.Warning));
    }
}
