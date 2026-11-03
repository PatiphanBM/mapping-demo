using System.Diagnostics;
using System.Globalization;
using System.Text;
using ExcelDataReader;

namespace MappingDemo.Shared.Input;

internal static class ExcelWorkbookReader
{
    private static readonly byte[] XlsSignature =
        [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];
    private static readonly Encoding Windows1252;
    private static readonly Encoding Windows874;

    static ExcelWorkbookReader()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Windows1252 = Encoding.GetEncoding(
            1252,
            EncoderFallback.ExceptionFallback,
            DecoderFallback.ExceptionFallback);
        Windows874 = Encoding.GetEncoding(
            874,
            EncoderFallback.ExceptionFallback,
            DecoderFallback.ExceptionFallback);
    }

    internal static async Task<InputRecordReadResult> ReadAsync(
        Stream input,
        ExcelInputLayout layout,
        string? fileExtension,
        InputReadLimits limits,
        CancellationToken cancellationToken)
    {
        Stream workbookStream = input;
        MemoryStream? buffered = null;
        try
        {
            if (!input.CanSeek)
            {
                buffered = new MemoryStream();
                await input.CopyToAsync(buffered, cancellationToken);
                buffered.Position = 0;
                workbookStream = buffered;
            }

            if (workbookStream.Length > limits.MaxBytes)
            {
                return Fail("WorkbookTooLarge");
            }

            var signature = DetectSignature(workbookStream);
            var expected = ResolveExpectedKind(layout, fileExtension);
            if (signature is null)
            {
                return Fail("InvalidWorkbookSignature");
            }

            if (expected is not null && signature != expected)
            {
                return Fail("WorkbookSignatureMismatch");
            }

            workbookStream.Position = 0;
            using var reader = ExcelReaderFactory.CreateReader(
                workbookStream,
                new ExcelReaderConfiguration
                {
                    LeaveOpen = true,
                    FallbackEncoding = Encoding.GetEncoding(1252),
                    SinglePassMode = true
                });
            return ReadWorkbook(
                reader,
                layout,
                limits,
                signature == ExcelWorkbookKind.Xls,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException)
        {
            return Fail("WorkbookReadTimeLimitExceeded");
        }
        catch (Exception exception)
            when (exception is IOException or InvalidOperationException or
                  NotSupportedException or UnauthorizedAccessException)
        {
            var encrypted = exception.Message.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                            exception.Message.Contains("encrypt", StringComparison.OrdinalIgnoreCase);
            return Fail(encrypted ? "EncryptedWorkbook" : "CorruptWorkbook");
        }
        finally
        {
            buffered?.Dispose();
        }
    }

    private static InputRecordReadResult ReadWorkbook(
        IExcelDataReader reader,
        ExcelInputLayout layout,
        InputReadLimits limits,
        bool repairLegacyThai,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var stringDecoder = new LegacyThaiStringDecoder(repairLegacyThai);
        var sheets = new List<string>();
        var records = new List<InputRecord>();
        var headers = new List<string>();
        var selected = false;

        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sheetName = string.IsNullOrEmpty(reader.Name)
                ? $"Sheet{sheets.Count + 1}"
                : reader.Name;
            sheets.Add(sheetName);
            if (!string.Equals(sheetName, layout.Sheet, StringComparison.Ordinal))
            {
                while (reader.Read())
                {
                    CheckElapsed(stopwatch, limits);
                }
                continue;
            }

            selected = true;
            var headerIndexes = new Dictionary<string, int>(StringComparer.Ordinal);
            var worksheetRow = 0;
            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                CheckElapsed(stopwatch, limits);
                worksheetRow++;
                if (reader.FieldCount > limits.MaxColumns)
                {
                    return Fail("TooManyWorkbookColumns", worksheetRow);
                }

                if (worksheetRow == layout.HeaderRow)
                {
                    var seen = new HashSet<string>(StringComparer.Ordinal);
                    for (var column = 0; column < reader.FieldCount; column++)
                    {
                        if (reader.GetCellError(column) is not null)
                        {
                            return FailCellError(worksheetRow, column + 1);
                        }
                        var value = FormatCell(
                            reader.GetValue(column),
                            stringDecoder);
                        headers.Add(value);
                        if (value.Length > limits.MaxCellCharacters)
                        {
                            return Fail("CellTooLong", worksheetRow);
                        }
                        if (!string.IsNullOrEmpty(value) && !seen.Add(value))
                        {
                            return Fail("DuplicateWorkbookHeader", worksheetRow);
                        }
                        if (!string.IsNullOrEmpty(value))
                        {
                            headerIndexes[value] = column;
                        }
                    }

                    foreach (var field in layout.Fields)
                    {
                        if (!headerIndexes.ContainsKey(field.InputHeader))
                        {
                            return Fail("RequiredWorkbookHeaderMissing", worksheetRow);
                        }
                    }
                    continue;
                }

                if (worksheetRow < layout.DataStartRow)
                {
                    continue;
                }

                if (records.Count >= limits.MaxRows)
                {
                    return Fail("TooManyWorkbookRows", worksheetRow);
                }

                var values = new Dictionary<string, string>(StringComparer.Ordinal);
                var raw = new string[reader.FieldCount];
                var blank = true;
                for (var column = 0; column < reader.FieldCount; column++)
                {
                    if (reader.GetCellError(column) is not null)
                    {
                        return FailCellError(worksheetRow, column + 1);
                    }
                    var cell = FormatCell(
                        reader.GetValue(column),
                        stringDecoder);
                    if (cell.Length > limits.MaxCellCharacters)
                    {
                        return Fail("CellTooLong", worksheetRow);
                    }
                    raw[column] = cell;
                    blank &= string.IsNullOrEmpty(cell);
                }

                if (blank)
                {
                    if (layout.BlankRowPolicy == ExcelBlankRowPolicy.Error)
                    {
                        return Fail("BlankWorkbookRow", worksheetRow);
                    }
                    continue;
                }

                foreach (var field in layout.Fields)
                {
                    var column = headerIndexes[field.InputHeader];
                    values[field.SourceColumn] = column < raw.Length ? raw[column] : string.Empty;
                }

                records.Add(new InputRecord(
                    records.Count + 1,
                    worksheetRow,
                    InputRecordAction.Import,
                    null,
                    values,
                    string.Join('\t', raw),
                    worksheetRow));
            }
        } while (reader.NextResult());

        if (!selected)
        {
            return InputRecordReadResult.Failure(new InputRecordReadError(
                "WorkbookSheetNotFound",
                0,
                Scalar: string.Join("|", sheets)));
        }

        if (headers.Count == 0)
        {
            return Fail("WorkbookHeaderRowNotFound", layout.HeaderRow);
        }

        return InputRecordReadResult.Success(
            records,
            new WorkbookReadMetadata(sheets, layout.Sheet, headers));
    }

    private static void CheckElapsed(Stopwatch stopwatch, InputReadLimits limits)
    {
        if (limits.MaxElapsed is { } maximum && stopwatch.Elapsed > maximum)
        {
            throw new TimeoutException("Workbook read time limit exceeded.");
        }
    }

    private static string FormatCell(
        object? value,
        LegacyThaiStringDecoder stringDecoder) => value switch
        {
            null => string.Empty,
            string text => stringDecoder.Decode(text),
            bool boolean => boolean ? "true" : "false",
            DateTime dateTime when dateTime.TimeOfDay == TimeSpan.Zero =>
                dateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTime dateTime => dateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff", CultureInfo.InvariantCulture),
            TimeSpan time => time.ToString("c", CultureInfo.InvariantCulture),
            double number => number.ToString("G17", CultureInfo.InvariantCulture),
            float number => number.ToString("G9", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => value.ToString() ?? string.Empty
        };

    private static ExcelWorkbookKind? DetectSignature(Stream stream)
    {
        Span<byte> header = stackalloc byte[8];
        stream.Position = 0;
        var count = stream.Read(header);
        stream.Position = 0;
        if (count >= XlsSignature.Length && header.SequenceEqual(XlsSignature))
        {
            return ExcelWorkbookKind.Xls;
        }
        if (count >= 4 && header[0] == 0x50 && header[1] == 0x4B &&
            header[2] is 0x03 or 0x05 or 0x07 &&
            header[3] is 0x04 or 0x06 or 0x08)
        {
            return ExcelWorkbookKind.Xlsx;
        }
        if (count >= 4 && header[0] == 0x09 &&
            header[1] is 0x00 or 0x02 or 0x04 or 0x08)
        {
            return ExcelWorkbookKind.Xls;
        }
        return null;
    }

    private static ExcelWorkbookKind? ResolveExpectedKind(
        ExcelInputLayout layout,
        string? extension)
    {
        if (layout.WorkbookKind != ExcelWorkbookKind.AutoDetect)
        {
            return layout.WorkbookKind;
        }

        var normalized = extension is null
            ? null
            : InputFileExtensionPolicy.Normalize(extension);
        return normalized switch
        {
            ".xlsx" => ExcelWorkbookKind.Xlsx,
            ".xls" => ExcelWorkbookKind.Xls,
            _ => null
        };
    }

    private static InputRecordReadResult Fail(string code, int row = 0) =>
        InputRecordReadResult.Failure(new InputRecordReadError(code, row));

    private static InputRecordReadResult FailCellError(int row, int column) =>
        InputRecordReadResult.Failure(new InputRecordReadError(
            "WorkbookCellError",
            row,
            Position: column));

    private sealed class LegacyThaiStringDecoder(bool enabled)
    {
        private bool _thaiDetected;

        internal string Decode(string value)
        {
            if (!enabled || value.All(character => character <= 0x7F))
            {
                return value;
            }

            string candidate;
            try
            {
                candidate = Windows874.GetString(Windows1252.GetBytes(value));
            }
            catch (EncoderFallbackException)
            {
                return value;
            }

            if (!_thaiDetected && !LooksLikeThai(candidate))
            {
                return value;
            }

            _thaiDetected = true;
            return candidate;
        }

        private static bool LooksLikeThai(string value)
        {
            var visible = value.Count(character => !char.IsWhiteSpace(character));
            var thai = value.Count(character => character is >= '\u0E00' and <= '\u0E7F');
            return thai >= 3 && thai * 2 >= visible;
        }
    }
}
