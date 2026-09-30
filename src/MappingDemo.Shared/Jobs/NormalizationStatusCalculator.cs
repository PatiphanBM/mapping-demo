namespace MappingDemo.Shared.Jobs;

public static class NormalizationStatusCalculator
{
    public static string Calculate(
        string importStatus,
        int pendingRows,
        int doneRows,
        int invalidRows,
        int failedRows)
    {
        if (importStatus != ImportStatus.Imported || pendingRows > 0)
        {
            return NormalizationStatus.InProgress;
        }

        if (invalidRows > 0 || failedRows > 0)
        {
            return NormalizationStatus.CompletedWithErrors;
        }

        return NormalizationStatus.Completed;
    }
}
