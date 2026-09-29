using System.Globalization;
using System.Runtime.CompilerServices;
using CsvHelper;

namespace MappingDemo.Shared.Csv;

public sealed class CsvRecordReader
{
    public async Task<IReadOnlyList<string>> ReadHeaderAsync(
        TextReader textReader,
        CancellationToken cancellationToken = default)
    {
        using var csv = new CsvReader(
            textReader,
            CultureInfo.InvariantCulture);

        if (!await csv.ReadAsync().WaitAsync(cancellationToken))
        {
            return [];
        }

        csv.ReadHeader();
        return csv.HeaderRecord ?? [];
    }

    public async IAsyncEnumerable<CsvRecord> ReadAsync(
        TextReader textReader,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var csv = new CsvReader(
            textReader,
            CultureInfo.InvariantCulture);

        if (!await csv.ReadAsync().WaitAsync(cancellationToken))
        {
            yield break;
        }

        csv.ReadHeader();
        var headers = csv.HeaderRecord ?? [];
        var rowNumber = 0;

        while (await csv.ReadAsync().WaitAsync(cancellationToken))
        {
            var values = headers.ToDictionary(
                header => header,
                header => csv.GetField(header) ?? string.Empty,
                StringComparer.Ordinal);

            yield return new CsvRecord(++rowNumber, values);
        }
    }
}
