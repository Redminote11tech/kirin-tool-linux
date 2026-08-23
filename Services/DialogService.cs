using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace Kirin_Tool.Services;

/// <summary>
/// Cross-platform dialog service replacing WPF-UI's ContentDialogService.
/// Shows Avalonia Windows as dialogs (modal or non-modal).
/// </summary>
public class DialogService
{
    private readonly Window _owner;

    public DialogService(Window owner)
    {
        _owner = owner;
    }

    /// <summary>
    /// Show a dialog window modally and return its result.
    /// </summary>
    public Task<bool?> ShowDialog(Window dialog)
    {
        return dialog.ShowDialog<bool?>(_owner);
    }

    /// <summary>
    /// Show a dialog window non-modally (for progress dialogs that run alongside background work).
    /// Returns a Task that completes when the dialog is closed.
    /// </summary>
    public Task ShowNonModalDialog(Window dialog)
    {
        var tcs = new TaskCompletionSource();
        dialog.Closed += (_, _) => tcs.TrySetResult();
        dialog.Show(_owner);
        return tcs.Task;
    }

    /// <summary>
    /// Show a simple message box with an OK button.
    /// </summary>
    public async Task ShowMessageBox(string title, string message)
    {
        var dialog = new Window
        {
            Title = title,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SizeToContent = SizeToContent.WidthAndHeight,
            MinWidth = 300,
            MaxWidth = 500,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new(24),
                Spacing = 16,
                Children =
                {
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 14
                    },
                    new Button
                    {
                        Content = "OK",
                        HorizontalAlignment = HorizontalAlignment.Right,
                        MinWidth = 100,
                        Height = 32
                    }
                }
            }
        };

        // Wire the OK button
        var stack = (StackPanel)dialog.Content;
        ((Button)stack.Children[1]).Click += (_, _) => dialog.Close();

        await dialog.ShowDialog<bool?>(_owner);
    }

    /// <summary>
    /// Show a simple confirmation dialog with Primary and Close buttons.
    /// Returns true if Primary was clicked.
    /// </summary>
    public async Task<bool> ShowConfirmDialog(string title, string message, string primaryText = "Continue", string closeText = "Cancel")
    {
        var result = false;
        var dialog = new Window
        {
            Title = title,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SizeToContent = SizeToContent.WidthAndHeight,
            MinWidth = 350,
            MaxWidth = 500,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new(24),
                Spacing = 16,
                Children =
                {
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 14
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Children =
                        {
                            new Button
                            {
                                Content = closeText,
                                MinWidth = 100,
                                Height = 32
                            },
                            new Button
                            {
                                Content = primaryText,
                                MinWidth = 100,
                                Height = 32
                            }
                        }
                    }
                }
            }
        };

        var outerStack = (StackPanel)dialog.Content;
        var buttonRow = (StackPanel)outerStack.Children[1];
        ((Button)buttonRow.Children[0]).Click += (_, _) => { result = false; dialog.Close(); };
        ((Button)buttonRow.Children[1]).Click += (_, _) => { result = true; dialog.Close(); };

        await dialog.ShowDialog<bool?>(_owner);
        return result;
    }

    /// <summary>
    /// Show a SaveFileDialog and return the selected path, or null if cancelled.
    /// </summary>
    public async Task<string?> ShowSaveFileDialog(string title, string? defaultName = null, string? filterName = null, List<string>? extensions = null)
    {
        var topLevel = TopLevel.GetTopLevel(_owner);
        if (topLevel?.StorageProvider == null) return null;

        var options = new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = defaultName ?? ""
        };

        if (filterName != null && extensions != null && extensions.Count > 0)
        {
            options.FileTypeChoices = new List<FilePickerFileType>
            {
                new FilePickerFileType(filterName)
                {
                    Patterns = extensions.ConvertAll(ext => $"*{ext}")
                }
            };
        }

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(options);
        return file?.Path.LocalPath;
    }
    /// <summary>
    /// Show an OpenFileDialog and return the selected path, or null if cancelled.
    /// </summary>
    public async Task<string?> ShowOpenFileDialog(string title, List<string>? extensions = null, string? filterName = null)
    {
        var topLevel = TopLevel.GetTopLevel(_owner);
        if (topLevel?.StorageProvider == null) return null;

        var options = new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        };

        if (extensions != null && extensions.Count > 0)
        {
            options.FileTypeFilter = new List<FilePickerFileType>
            {
                new FilePickerFileType(filterName ?? "Files")
                {
                    Patterns = extensions.ConvertAll(ext => $"*{ext}")
                }
            };
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(options);
        return files?.FirstOrDefault()?.Path.LocalPath;
    }
}
