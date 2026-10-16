using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

namespace MappingDemo.Shared.Input;

internal static class DelimitedInputReader
{
    internal static async Task<InputRecordReadResult> ReadAsync(
        string text,
        DelimitedInputLayout layout,
        CancellationToken cancellationToken)
    {
        using var textReader = new StringReader(text);
        using var csv = CreateReader(textReader, layout.Delimiter);

        try
        {
            for (var index = 0; index < layout.PreambleLines; index++)
            {
                if (!await ReadNextAsync(csv, cancellationToken))
                {
                    return InputRecordReadResult.Success([]);
                }
            }

            if (layout.HeaderPolicy == DelimitedHeaderPolicy.SingleRow)
            {
                if (!await ReadNextAsync(csv, cancellationToken))
                {
                    return InputRecordReadResult.Success([]);
                }

                csv.ReadHeader();
            }

            var records = new List<InputRecord>();
            var rowNumber = 0;
            while (true)
            {
                var physicalLineNumber = csv.Parser.RawRow + 1;
                if (!await ReadNextAsync(csv, cancellationToken))
                {
                    break;
                }

                var values = new Dictionary<string, string>(
                    StringComparer.Ordinal);
                for (var index = 0; index < layout.Fields.Count; index++)
                {
                    var field = layout.Fields[index];
                    var value = layout.HeaderPolicy ==
                                DelimitedHeaderPolicy.SingleRow
                        ? csv.GetField(field.InputHeader)
                        : csv.GetField(index);
                    values.Add(field.SourceColumn, value ?? string.Empty);
                }

                records.Add(new InputRecord(
                    ++rowNumber,
                    physicalLineNumber,
                    InputRecordAction.Import,
                    RecordRule: null,
                    values,
                    csv.Parser.RawRecord.TrimEnd('\r', '\n')));
            }

            return InputRecordReadResult.Success(records);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (CsvHelperException)
        {
            return InputRecordReadResult.Failure(
                new InputRecordReadError(
                    "InvalidDelimitedRecord",
                    Math.Max(1, csv.Parser.RawRow)));
        }
    }

    private static CsvReader CreateReader(
        TextReader textReader,
        string delimiter)
    {
        var configuration = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimiter
        };
        return new CsvReader(textReader, configuration);
    }

    private static async Task<bool> ReadNextAsync(
        CsvReader csv,
        CancellationToken cancellationToken)
    {
        return await csv.ReadAsync().WaitAsync(cancellationToken);
    }
}
