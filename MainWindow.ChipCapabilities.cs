// Licensed under the repository's Business Source License 1.1. See LICENSE.
using Avalonia.Controls;
using Kirin_Tool.Services;
namespace Kirin_Tool;

public partial class MainWindow
{
    private static string FlashFailureDescription(string output)
    {
        var failed = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(line => line.Contains("FAILED", StringComparison.OrdinalIgnoreCase));
        return failed?.Trim() ?? "Flash failed; see the fastboot log.";
    }
    private async Task<bool> AllowChipOperationAsync(string operation)
    {
        if (ChipCapabilities.Allows(GetSelectedCpu(), operation)) return true;
        await _dialogService.ShowMessageBox("Notice", ChipCapabilities.UnsupportedNotice, showHeading: true);
        return false;
    }
    private async void ReadPsid_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!await AllowChipOperationAsync("read-psid")) return;
        try
        {
            if (!await _fastbootClient.IsDeviceConnected())
            {
                await ShowMessageBox("Device Not Found", "No device detected in fastboot mode.");
                return;
            }
            var values = await _fastbootClient.ReadPsidAsync();
            string text = string.Join("\n", values.Select(pair => $"{pair.Key}: {pair.Value}"));
            await ShowMessageBox("Device identifiers (get-psid)", text);
        }
        catch (Exception ex) { await ShowMessageBox("Read device identifiers", ex.Message); }
    }
}
