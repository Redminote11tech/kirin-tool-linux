// UPDATE.APP extraction uses the existing BSL-1.1 reader; see LICENSE.
using System.IO;
using Kirin_Tool.Models;
using Kirin_Tool.Utils;

namespace Kirin_Tool.Services;

public sealed record ExtractionEntry(PartitionInfo Partition, string FileName)
{
    public string Name => Partition.Name;
    public string FormattedSize => Partition.FormattedSize;
    public bool IsSelected { get => Partition.IsSelected; set => Partition.IsSelected = value; }
}

public sealed record ExtractionProgress(string Name, int Completed, int Total, long Bytes, long TotalBytes)
{
    public int Index { get; init; }
    public long PartitionBytes { get; init; }
    public long PartitionSize { get; init; }
    public string State { get; init; } = "Extracting";
}

public static class PartitionExtractor
{
    public static List<ExtractionEntry> Read(string path, CancellationToken cancellationToken = default)
    {
        using var archive = new UpdateApp(path, isUsbMode: true, strict: true, cancellationToken: cancellationToken);
        return MakeEntries(archive.Partitions);
    }

    private static List<ExtractionEntry> MakeEntries(List<PartitionInfo> partitions)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<ExtractionEntry>();
        foreach (var partition in partitions)
        {
            string stem = partition.Name;
            string fileName = stem + ".img";
            int suffix = 2;
            while (!used.Add(fileName)) fileName = $"{stem}_{suffix++}.img";
            result.Add(new ExtractionEntry(partition, fileName));
        }
        return result;
    }

    public static async Task<int> ExtractAsync(string source, string destination, IReadOnlyList<ExtractionEntry> displayed,
        CancellationToken cancellationToken = default, Action<ExtractionProgress>? onProgress = null)
    {
        var selected = displayed.Select((entry, index) => (entry, index)).Where(x => x.entry.IsSelected).ToList();
        if (selected.Count == 0) throw new InvalidOperationException("Select at least one partition to extract.");
        using var archive = new UpdateApp(source, isUsbMode: true, strict: true, cancellationToken: cancellationToken);
        var current = MakeEntries(archive.Partitions);
        if (current.Count != displayed.Count || current.Where((e, i) => e.Name != displayed[i].Name ||
            e.Partition.DataOffset != displayed[i].Partition.DataOffset || e.Partition.Size != displayed[i].Partition.Size ||
            e.Partition.HeaderSize != displayed[i].Partition.HeaderSize || e.FileName != displayed[i].FileName).Any())
            throw new InvalidDataException("UPDATE.APP changed. Browse the file again before extracting.");
        destination = Path.GetFullPath(destination);
        Directory.CreateDirectory(destination);
        foreach (var (entry, index) in selected)
        {
            string target = Path.Combine(destination, current[index].FileName);
            if (Path.Exists(target) || new FileInfo(target).LinkTarget != null)
            {
                onProgress?.Invoke(new(entry.Name, 0, selected.Count, 0, 0)
                    { Index = selected.FindIndex(x => x.index == index), State = "Failed" });
                throw new IOException($"Output already exists: {current[index].FileName}. Choose another folder or remove the existing file.");
            }
        }
        long totalBytes = selected.Sum(x => current[x.index].Partition.Size);
        long bytes = 0;
        int completed = 0;
        for (int i = 0; i < selected.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = current[selected[i].index];
            long partitionBytes = 0;
            void Report(string state = "Extracting") => onProgress?.Invoke(new(entry.Name, completed, selected.Count, bytes, totalBytes)
                { Index = i, PartitionBytes = partitionBytes, PartitionSize = entry.Partition.Size, State = state });
            Report();
            try
            {
                await archive.ExtractPartitionSafelyAsync(entry.Partition, Path.Combine(destination, entry.FileName), cancellationToken,
                    count => { bytes += count; partitionBytes += count; Report(); });
                completed++;
                Report("Extracted");
            }
            catch (OperationCanceledException) { Report("Cancelled"); throw; }
            catch { Report("Failed"); throw; }
        }
        return completed;
    }
}
