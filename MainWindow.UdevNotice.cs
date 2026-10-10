// Licensed under the repository's Business Source License 1.1. See LICENSE.
using Avalonia.Controls;
using Avalonia.Interactivity;
using Kirin_Tool.Services;

namespace Kirin_Tool;

public partial class MainWindow
{
    private bool _checkingUdevRules;
    private async Task RefreshUdevNoticeAsync()
    {
        if (!UdevRuleNotice.IsAppImage || _checkingUdevRules) return;
        _checkingUdevRules = true;
        try { RenderUdevNotice(await Task.Run(() => UdevRuleNotice.Inspect())); }
        finally { _checkingUdevRules = false; }
    }
    private void RenderUdevNotice(UdevRuleCoverage coverage)
    {
        this.FindControl<Border>("UdevNotice")!.IsVisible = UdevRuleNotice.IsAppImage && !coverage.Complete;
        this.FindControl<TextBox>("UdevCommands")!.Text = UdevRuleNotice.ApplyCommands;
        this.FindControl<TextBlock>("UdevNoticeDescription")!.Text = coverage.ReadError
            ? "Some udev rules could not be read. Apply these desktop USB permissions if needed, then reconnect your device."
            : "Huawei USB or VCOM permission rules are missing. Copy and run these commands in a terminal, then reconnect your device.";
    }
    private async void CopyUdevCommands_Click(object? sender, RoutedEventArgs e)
    {
        if (Clipboard == null) return;
        try
        {
            await Clipboard.SetTextAsync(UdevRuleNotice.ApplyCommands);
            this.FindControl<TextBlock>("UdevCopyStatus")!.Text = "Commands copied";
        }
        catch { this.FindControl<TextBlock>("UdevCopyStatus")!.Text = "Select the commands above to copy them manually."; }
    }
    private async void CheckUdevRules_Click(object? sender, RoutedEventArgs e) => await RefreshUdevNoticeAsync();
}
