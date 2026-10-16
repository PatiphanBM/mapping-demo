using MappingDemo.Shared.Input;
using MappingDemo.Shared.Tables;
using MappingDemo.Shared.Transformation;

namespace MappingDemo.Shared.MappingConfigs;

public static class ConfigVersionValidator
{
    public static IReadOnlyList<ConfigVersionValidationError> Validate(
        InputLayoutDefinition inputLayout,
        TransformationProgram transformationProgram,
        TableDefinition sourceTable,
        TableDefinition normalizedTable)
    {
        ArgumentNullException.ThrowIfNull(inputLayout);
        ArgumentNullException.ThrowIfNull(transformationProgram);
        ArgumentNullException.ThrowIfNull(sourceTable);
        ArgumentNullException.ThrowIfNull(normalizedTable);

        var errors = new List<ConfigVersionValidationError>();
        ValidateInputLayout(inputLayout, sourceTable, errors);
        ValidateTransformationProgram(
            transformationProgram,
            sourceTable,
            normalizedTable,
            errors);

        return errors;
    }

    private static void ValidateInputLayout(
        InputLayoutDefinition inputLayout,
        TableDefinition sourceTable,
        ICollection<ConfigVersionValidationError> errors)
    {
        var result = new InputRecordModule().Compile(
            inputLayout,
            sourceTable);
        foreach (var diagnostic in result.Diagnostics.Where(diagnostic =>
                     diagnostic.Severity ==
                     InputLayoutDiagnosticSeverity.Error))
        {
            errors.Add(new ConfigVersionValidationError(
                $"inputLayout.{diagnostic.Path}",
                diagnostic.Message));
        }
    }

    private static void ValidateTransformationProgram(
        TransformationProgram program,
        TableDefinition sourceTable,
        TableDefinition normalizedTable,
        ICollection<ConfigVersionValidationError> errors)
    {
        var result = new TransformationModule().Compile(
            program,
            new TransformationSchema(sourceTable, normalizedTable));
        foreach (var diagnostic in result.Diagnostics.Where(diagnostic =>
                     diagnostic.Severity ==
                     TransformationDiagnosticSeverity.Error))
        {
            errors.Add(new ConfigVersionValidationError(
                $"transformationProgram.{diagnostic.Path}",
                diagnostic.Message));
        }
    }
}
