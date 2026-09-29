using System.Globalization;
using MappingDemo.Shared.Tables;

namespace MappingDemo.Shared.Normalization;

public static class ValueConverter
{
    public static ConversionResult Convert(
        string? value,
        ColumnDataType dataType,
        bool isRequired,
        string? format = null)
    {
        var trimmedValue = value?.Trim() ?? string.Empty;

        if (trimmedValue.Length == 0)
        {
            return isRequired
                ? new ConversionResult.Failure("Required")
                : new ConversionResult.Success(null);
        }

        return dataType switch
        {
            ColumnDataType.Text => new ConversionResult.Success(trimmedValue),
            ColumnDataType.Date => ConvertDate(trimmedValue, format),
            ColumnDataType.Decimal => ConvertDecimal(trimmedValue),
            ColumnDataType.Boolean => ConvertBoolean(trimmedValue),
            _ => throw new NotSupportedException(
                $"Conversion for {dataType} is not supported.")
        };
    }

    private static ConversionResult ConvertDate(string value, string? format)
    {
        var dateFormat = format ?? "yyyy-MM-dd";

        return DateOnly.TryParseExact(
            value,
            dateFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var convertedValue)
            ? new ConversionResult.Success(convertedValue)
            : new ConversionResult.Failure("InvalidDate");
    }

    private static ConversionResult ConvertDecimal(string value)
    {
        const NumberStyles styles =
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;

        return decimal.TryParse(
            value,
            styles,
            CultureInfo.InvariantCulture,
            out var convertedValue)
            ? new ConversionResult.Success(convertedValue)
            : new ConversionResult.Failure("InvalidDecimal");
    }

    private static ConversionResult ConvertBoolean(string value)
    {
        if (value == "1")
        {
            return new ConversionResult.Success(true);
        }

        if (value == "0")
        {
            return new ConversionResult.Success(false);
        }

        return bool.TryParse(value, out var convertedValue)
            ? new ConversionResult.Success(convertedValue)
            : new ConversionResult.Failure("InvalidBoolean");
    }
}
