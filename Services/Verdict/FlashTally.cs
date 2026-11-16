namespace Kirin_Tool.Services.Verdict;

/// <summary>
/// Turns per-partition outcomes into one flash verdict (debt D-05 / audit F03, S17).
///
/// The GUI used to count a skipped partition as a success in the same counter as a written one,
/// so <c>successCount == totalCount</c> held even when every redline member was skipped and
/// nothing was written — and the dialog then said "Successfully flashed all N partitions!".
/// Skips are a third outcome here: they are reported, they never make the run a success, and
/// they are excluded from the denominator so the message cannot overstate either way.
/// </summary>
public sealed record FlashTallyResult(
    bool AllSuccessful,
    int SuccessCount,
    int SkippedCount,
    int FailedCount,
    int AttemptedCount,
    int TotalCount,
    string Message);

public static class FlashTally
{
    public static FlashTallyResult Describe(int successCount, int skippedCount, int totalCount)
    {
        if (successCount < 0) successCount = 0;
        if (skippedCount < 0) skippedCount = 0;
        if (totalCount < 0) totalCount = 0;
        if (successCount + skippedCount > totalCount) successCount = totalCount - skippedCount;

        int attempted = totalCount - skippedCount;
        int failed = attempted - successCount;
        bool allSuccessful = attempted > 0 && failed == 0;

        string message;
        if (skippedCount > 0)
        {
            message = allSuccessful
                ? $"Flashed {successCount}/{attempted} partitions; {skippedCount} skipped (not written)."
                : $"Flash completed with {successCount}/{attempted} successful; {skippedCount} skipped (not written).";
        }
        else
        {
            message = allSuccessful
                ? $"Successfully flashed all {totalCount} partitions!"
                : $"Flash completed with {successCount}/{totalCount} successful.";
        }

        return new FlashTallyResult(allSuccessful, successCount, skippedCount, failed, attempted, totalCount, message);
    }
}
