namespace MappingDemo.Shared.Input;

internal static class FixedWidthInputReader
{
    internal static InputRecordReadResult Read(
        string text,
        FixedWidthInputLayout layout)
    {
        var lines = SplitLines(text);
        var characterError = ValidateCharacters(lines);
        if (characterError is not null)
        {
            return InputRecordReadResult.Failure(characterError);
        }

        var records = new List<InputRecord>(lines.Count);
        var sequenceCounts = new int[layout.RecordSequence.Count];
        var sequenceIndex = 0;
        var rowNumber = 0;

        foreach (var line in lines)
        {
            var rule = FindRule(line.Text, layout.RecordRules);
            if (rule is null)
            {
                return InputRecordReadResult.Failure(
                    new InputRecordReadError(
                        "UnknownRecordType",
                        line.Number,
                        Discriminator: FirstCharacter(line.Text)));
            }

            if (line.Text.Length != rule.ExpectedLength)
            {
                return InputRecordReadResult.Failure(
                    new InputRecordReadError(
                        "UnexpectedRecordLength",
                        line.Number,
                        rule.Key,
                        Expected: rule.ExpectedLength,
                        Actual: line.Text.Length));
            }

            var sequenceError = AdvanceSequence(
                layout.RecordSequence,
                sequenceCounts,
                ref sequenceIndex,
                rule.Key,
                line.Number);
            if (sequenceError is not null)
            {
                return InputRecordReadResult.Failure(sequenceError);
            }

            IReadOnlyDictionary<string, string> values;
            int? importedRowNumber;
            if (rule.Action == InputRecordAction.Import)
            {
                var slices = new Dictionary<string, string>(
                    StringComparer.Ordinal);
                foreach (var field in rule.Fields)
                {
                    slices.Add(
                        field.SourceColumn,
                        line.Text.Substring(field.Start - 1, field.Length));
                }

                values = slices;
                importedRowNumber = ++rowNumber;
            }
            else
            {
                values = new Dictionary<string, string>(StringComparer.Ordinal);
                importedRowNumber = null;
            }

            records.Add(new InputRecord(
                importedRowNumber,
                line.Number,
                rule.Action,
                rule.Key,
                values,
                line.Text));
        }

        var cardinalityError = ValidateMinimumCardinality(
            layout.RecordSequence,
            sequenceCounts,
            lines.Count);
        return cardinalityError is null
            ? InputRecordReadResult.Success(records)
            : InputRecordReadResult.Failure(cardinalityError);
    }

    private static FixedWidthRecordRule? FindRule(
        string text,
        IReadOnlyList<FixedWidthRecordRule> rules)
    {
        foreach (var rule in rules)
        {
            var discriminator = rule.Discriminator;
            var start = discriminator.Start - 1;
            if (start + discriminator.Length <= text.Length &&
                string.CompareOrdinal(
                    text,
                    start,
                    discriminator.Value,
                    0,
                    discriminator.Length) == 0)
            {
                return rule;
            }
        }

        return null;
    }

    private static InputRecordReadError? AdvanceSequence(
        IReadOnlyList<InputRecordSequenceItem> sequence,
        int[] counts,
        ref int sequenceIndex,
        string ruleKey,
        int physicalLineNumber)
    {
        var targetIndex = -1;
        for (var index = 0; index < sequence.Count; index++)
        {
            if (string.Equals(
                    sequence[index].RecordRule,
                    ruleKey,
                    StringComparison.Ordinal))
            {
                targetIndex = index;
                break;
            }
        }

        if (targetIndex < sequenceIndex)
        {
            return new InputRecordReadError(
                "UnexpectedRecordSequence",
                physicalLineNumber,
                ruleKey,
                ExpectedNext: sequence[sequenceIndex].RecordRule);
        }

        while (sequenceIndex < targetIndex)
        {
            var current = sequence[sequenceIndex];
            if (counts[sequenceIndex] < current.MinOccurs)
            {
                return new InputRecordReadError(
                    "UnexpectedRecordSequence",
                    physicalLineNumber,
                    ruleKey,
                    ExpectedNext: current.RecordRule);
            }

            sequenceIndex++;
        }

        var item = sequence[sequenceIndex];
        if (item.MaxOccurs is not null &&
            counts[sequenceIndex] >= item.MaxOccurs)
        {
            return new InputRecordReadError(
                "RecordCardinalityExceeded",
                physicalLineNumber,
                ruleKey,
                MaxOccurs: item.MaxOccurs);
        }

        counts[sequenceIndex]++;
        return null;
    }

    private static InputRecordReadError? ValidateMinimumCardinality(
        IReadOnlyList<InputRecordSequenceItem> sequence,
        IReadOnlyList<int> counts,
        int physicalLineCount)
    {
        for (var index = 0; index < sequence.Count; index++)
        {
            var item = sequence[index];
            if (counts[index] < item.MinOccurs)
            {
                return new InputRecordReadError(
                    "RecordCardinalityNotMet",
                    physicalLineCount,
                    item.RecordRule,
                    Actual: counts[index],
                    MinOccurs: item.MinOccurs);
            }
        }

        return null;
    }

    private static InputRecordReadError? ValidateCharacters(
        IReadOnlyList<PhysicalLine> lines)
    {
        foreach (var line in lines)
        {
            for (var index = 0; index < line.Text.Length; index++)
            {
                var character = line.Text[index];
                if (character == '\uFEFF')
                {
                    return new InputRecordReadError(
                        "UnexpectedByteOrderMark",
                        line.Number,
                        Position: index + 1);
                }

                if (!char.IsSurrogate(character))
                {
                    continue;
                }

                var scalar = char.IsHighSurrogate(character) &&
                             index + 1 < line.Text.Length &&
                             char.IsLowSurrogate(line.Text[index + 1])
                    ? char.ConvertToUtf32(character, line.Text[index + 1])
                    : character;
                return new InputRecordReadError(
                    "UnsupportedCharacterForPositionUnit",
                    line.Number,
                    Position: index + 1,
                    Scalar: $"U+{scalar:X}");
            }
        }

        return null;
    }

    private static IReadOnlyList<PhysicalLine> SplitLines(string text)
    {
        var lines = new List<PhysicalLine>();
        var lineStart = 0;
        var lineNumber = 1;

        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] != '\n')
            {
                continue;
            }

            var length = index - lineStart;
            if (length > 0 && text[index - 1] == '\r')
            {
                length--;
            }

            lines.Add(new PhysicalLine(
                lineNumber++,
                text.Substring(lineStart, length)));
            lineStart = index + 1;
        }

        if (lineStart < text.Length)
        {
            lines.Add(new PhysicalLine(
                lineNumber,
                text[lineStart..]));
        }

        return lines;
    }

    private static string FirstCharacter(string text)
    {
        return text.Length == 0 ? string.Empty : text[..1];
    }

    private sealed record PhysicalLine(int Number, string Text);
}
