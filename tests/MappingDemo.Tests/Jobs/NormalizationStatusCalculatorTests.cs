using MappingDemo.Shared.Jobs;

namespace MappingDemo.Tests.Jobs;

public sealed class NormalizationStatusCalculatorTests
{
    [Fact]
    public void Calculate_WhenImportIsNotFinished_ReturnsInProgress()
    {
        var result = NormalizationStatusCalculator.Calculate(
            ImportStatus.Importing,
            pendingRows: 0,
            doneRows: 10,
            invalidRows: 0,
            failedRows: 0);

        Assert.Equal(NormalizationStatus.InProgress, result);
    }

    [Fact]
    public void Calculate_WhenEveryRowIsDone_ReturnsCompleted()
    {
        var result = NormalizationStatusCalculator.Calculate(
            ImportStatus.Imported,
            pendingRows: 0,
            doneRows: 100,
            invalidRows: 0,
            failedRows: 0);

        Assert.Equal(NormalizationStatus.Completed, result);
    }

    [Fact]
    public void Calculate_WhenARowIsPending_ReturnsInProgress()
    {
        var result = NormalizationStatusCalculator.Calculate(
            ImportStatus.Imported,
            pendingRows: 1,
            doneRows: 99,
            invalidRows: 0,
            failedRows: 0);

        Assert.Equal(NormalizationStatus.InProgress, result);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(0, 1)]
    public void Calculate_WhenARowHasAnError_ReturnsCompletedWithErrors(
        int invalidRows,
        int failedRows)
    {
        var result = NormalizationStatusCalculator.Calculate(
            ImportStatus.Imported,
            pendingRows: 0,
            doneRows: 99,
            invalidRows,
            failedRows);

        Assert.Equal(NormalizationStatus.CompletedWithErrors, result);
    }
}
