// Licensed under the Business Source License 1.1. See LICENSE.
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Kirin_Tool.UI;

public partial class WindowCaption : UserControl
{
    public WindowCaption() => InitializeComponent();
    private Window? OwnerWindow => TopLevel.GetTopLevel(this) as Window;
    private void MinimizeWindow(object? sender, RoutedEventArgs e)
    { if (OwnerWindow is { } window) window.WindowState = WindowState.Minimized; }
    private void ToggleMaximize(object? sender, RoutedEventArgs e)
    { if (OwnerWindow is { } window) window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; }
    private void CloseWindow(object? sender, RoutedEventArgs e) => OwnerWindow?.Close();
    private void DragTitleBar(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || OwnerWindow is not { } window) return;
        if (e.ClickCount == 2) ToggleMaximize(sender, new RoutedEventArgs());
        else window.BeginMoveDrag(e);
    }
}
