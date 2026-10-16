using MappingDemo.Shared.Input;
using MappingDemo.Shared.MappingConfigs;
using MappingDemo.Shared.Tables;
using MappingDemo.Shared.Transformation;

namespace MappingDemo.Tests.MappingConfigs;

public sealed class ConfigVersionValidatorTests
{
    [Fact]
    public void Validate_prefixes_input_layout_diagnostic_paths()
    {
        var inputLayout = ValidInputLayout() with
        {
            Fields =
            [
                new DelimitedInputField("Order No", "missing_column"),
                .. ValidInputLayout().Fields.Skip(1)
            ]
        };

        var errors = ConfigVersionValidator.Validate(
            inputLayout,
            ValidProgram(),
            SourceTable(),
            NormalizedTable());

        Assert.Contains(
            errors,
            error => error.Field ==
                         "inputLayout.fields[0].sourceColumn" &&
                     error.Message == "Source column does not exist.");
    }

    [Fact]
    public void Validate_accepts_layout_and_transformation_program()
    {
        var errors = ConfigVersionValidator.Validate(
            ValidInputLayout(),
            ValidProgram(),
            SourceTable(),
            NormalizedTable());

        Assert.Empty(errors);
    }

    private static DelimitedInputLayout ValidInputLayout()
    {
        return new DelimitedInputLayout(
            "utf-8",
            ",",
            PreambleLines: 0,
            DelimitedHeaderPolicy.SingleRow,
            [
                new DelimitedInputField("Order No", "order_no"),
                new DelimitedInputField("Order Date", "order_date"),
                new DelimitedInputField("Amount", "amount")
            ]);
    }

    private static TransformationProgram ValidProgram()
    {
        return new TransformationProgram
        {
            LanguageVersion = 1,
            SemanticsProfile = "sqlserver-legacy-v1",
            Outputs =
            [
                new TransformationOutput
                {
                    OutputKey = "default",
                    Fields =
                    [
                        new FieldAssignment
                        {
                            NormalizedColumn = "order_no",
                            Value = new SourceExpression
                            {
                                Field = "order_no"
                            }
                        }
                    ]
                }
            ]
        };
    }

    private static TableDefinition SourceTable()
    {
        return new TableDefinition(
            "src_orders",
            TableKind.Source,
            [
                new ColumnDefinition(
                    "order_no",
                    ColumnDataType.Text,
                    IsRequired: false,
                    Ordinal: 1),
                new ColumnDefinition(
                    "order_date",
                    ColumnDataType.Text,
                    IsRequired: false,
                    Ordinal: 2),
                new ColumnDefinition(
                    "amount",
                    ColumnDataType.Text,
                    IsRequired: false,
                    Ordinal: 3)
            ]);
    }

    private static TableDefinition NormalizedTable()
    {
        return new TableDefinition(
            "norm_orders",
            TableKind.Normalized,
            [
                new ColumnDefinition(
                    "order_no",
                    ColumnDataType.Text,
                    IsRequired: true,
                    Ordinal: 1),
                new ColumnDefinition(
                    "order_date",
                    ColumnDataType.Date,
                    IsRequired: false,
                    Ordinal: 2),
                new ColumnDefinition(
                    "amount",
                    ColumnDataType.Decimal,
                    IsRequired: false,
                    Ordinal: 3)
            ]);
    }
}
