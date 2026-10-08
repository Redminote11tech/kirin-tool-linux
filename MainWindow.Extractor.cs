// Offline UPDATE.APP tools. See LICENSE for project licensing.
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Kirin_Tool.Services;
using Kirin_Tool.UI;

namespace Kirin_Tool;

public partial class MainWindow
{
    private List<ExtractionEntry> _extractionEntries = new();
    private string? _extractionSource;
    private string? _extractionDestination;
    private CancellationTokenSource? _extractionCancellation;

    private void SetExtractorBusy(bool busy)
    {
        ExtractorInputs.IsEnabled = !busy;
        ExtractorCancelButton.IsEnabled = busy;
        UpdateExtractionSelection();
    }

    private void UpdateExtractionSelection()
    {
        int selected = _extractionEntries.Count(e => e.IsSelected);
        ExtractorSelectionText.Text = $"{selected}/{_extractionEntries.Count} partitions selected";
        ExtractorStartButton.IsEnabled = _extractionCancellation == null && selected > 0 && _extractionDestination != null;
    }

    // Shared by the native file picker and the packaged headless UI check.
    private async Task LoadExtractionSourceAsync(string path)
    {
        _extractionCancellation = new CancellationTokenSource();
        SetExtractorBusy(true);
        _extractionSource = null;
        _extractionEntries = new();
        ExtractorPartitions.ItemsSource = _extractionEntries;
        ExtractorSourcePath.Text = path;
        ExtractorProgress.Value = 0;
        ExtractorStatus.Text = "Reading UPDATE.APP partitions…";
        try
        {
            var token = _extractionCancellation.Token;
            _extractionEntries = await Task.Run(() => PartitionExtractor.Read(path, token));
            _extractionSource = path;
            foreach (var entry in _extractionEntries)
                entry.Partition.PropertyChanged += (_, _) => UpdateExtractionSelection();
            ExtractorPartitions.ItemsSource = _extractionEntries;
            ExtractorStatus.Text = $"Found {_extractionEntries.Count} partitions. Choose the partitions and output folder.";
        }
        catch (OperationCanceledException) { ExtractorStatus.Text = "Reading cancelled."; }
        catch (Exception ex) { ExtractorStatus.Text = $"Could not read UPDATE.APP: {ex.Message}"; }
        finally
        {
            _extractionCancellation.Dispose();
            _extractionCancellation = null;
            SetExtractorBusy(false);
        }
    }

    private async void BrowseExtractionSource_Click(object? sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Base, Cust or Preload UPDATE.APP",
            AllowMultiple = false,
            Filters = new() { new FileDialogFilter { Name = "UPDATE.APP files", Extensions = new() { "app", "APP" } },
                new FileDialogFilter { Name = "All files", Extensions = new() { "*" } } }
        };
        var files = await dialog.ShowAsync(this);
        if (files is { Length: > 0 }) await LoadExtractionSourceAsync(files[0]);
    }

    private async void BrowseExtractionDestination_Click(object? sender, RoutedEventArgs e)
    {
        string? folder = await new OpenFolderDialog { Title = "Choose Extraction Folder" }.ShowAsync(this);
        if (string.IsNullOrWhiteSpace(folder)) return;
        _extractionDestination = folder;
        ExtractorDestinationPath.Text = folder;
        UpdateExtractionSelection();
    }

    private void SelectExtractionAll_Click(object? sender, RoutedEventArgs e)
    {
        foreach (var entry in _extractionEntries) entry.IsSelected = true;
    }

    private void SelectExtractionNone_Click(object? sender, RoutedEventArgs e)
    {
        foreach (var entry in _extractionEntries) entry.IsSelected = false;
    }

    private void CancelExtraction_Click(object? sender, RoutedEventArgs e) => _extractionCancellation?.Cancel();

    private async void StartExtraction_Click(object? sender, RoutedEventArgs e)
    {
        if (_extractionCancellation != null || _extractionSource == null || _extractionDestination == null) return;
        _extractionCancellation = new CancellationTokenSource();
        SetExtractorBusy(true);
        ExtractorProgress.Value = 0;
        int completed = 0;
        var selected = _extractionEntries.Where(e => e.IsSelected).ToList();
        var progressDialog = new ProcessDialogUapp(selected.Select(e => e.Partition).ToList(), _extractionCancellation);
        progressDialog.ConfigureExtraction();
        var dialogTask = progressDialog.ShowDialog(this);
        try
        {
            var token = _extractionCancellation.Token;
            // Throttle progress so large archives do not flood the UI dispatcher.
            long lastUpdate = 0;
            int count = await Task.Run(() => PartitionExtractor.ExtractAsync(_extractionSource, _extractionDestination,
                _extractionEntries, token, progress =>
                {
                    completed = progress.Completed;
                    long now = Environment.TickCount64;
                    if (now - lastUpdate < 100 && progress.State == "Extracting" && progress.PartitionBytes > 0) return;
                    lastUpdate = now;
                    Dispatcher.UIThread.Post(() =>
                    {
                        ExtractorProgress.Value = progress.TotalBytes == 0 ? 0 : 100.0 * progress.Bytes / progress.TotalBytes;
                        ExtractorStatus.Text = $"Extracting {progress.Name} — {progress.Completed}/{progress.Total} complete";
                        progressDialog.UpdateExtractionPartition(progress.Index, progress.State,
                            progress.PartitionSize == 0 ? 0 : 100.0 * progress.PartitionBytes / progress.PartitionSize,
                            ExtractorProgress.Value);
                    });
                }));
            // Drain previously posted progress before showing the final result.
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ExtractorProgress.Value = 100;
                ExtractorStatus.Text = $"Extracted {count} partitions to {_extractionDestination}.";
                progressDialog.FinishExtraction(ExtractorStatus.Text, true);
            });
        }
        catch (OperationCanceledException)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ExtractorStatus.Text = $"Extraction cancelled. {completed} complete images kept; unfinished image removed.";
                progressDialog.FinishExtraction(ExtractorStatus.Text, false);
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ExtractorStatus.Text = $"Extraction stopped: {ex.Message} ({completed} complete images kept).";
                progressDialog.FinishExtraction(ExtractorStatus.Text, false);
            });
        }
        finally
        {
            _extractionCancellation.Dispose();
            _extractionCancellation = null;
            SetExtractorBusy(false);
        }
        await dialogTask;
    }
}
