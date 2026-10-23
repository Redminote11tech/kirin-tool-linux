// Licensed under the Business Source License 1.1. See LICENSE.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Kirin_Tool.UI;

// Independently drawn outline geometry; no Windows font or WPF-UI assets required.
public static class WindowsIcons
{
    public static Geometry For(string? name) => Geometry.Parse(name switch
    {
        "Quick Start Guide" => "M3,10 L12,3 L21,10 M5,9 L5,21 L10,21 L10,14 L14,14 L14,21 L19,21 L19,9",
        "FB Lock" or "User Lock" or "Security Bypass" or "Unlocking" => "M6,10 L18,10 L18,21 L6,21 Z M8,10 L8,7 A4,4 0 0 1 16,7 L16,10 M12,14 L12,17",
        "Device Model" => "M7,2 L17,2 L17,22 L7,22 Z M10,18 L14,18",
        "Vendor / Country" => "M12,2 A10,10 0 1 1 11.99,2 M2,12 L22,12 M12,2 C6,7 6,17 12,22 C18,17 18,7 12,2",
        "Build Number" => "M9,3 L7,21 M17,3 L15,21 M3,8 L21,8 M2,16 L20,16",
        "Device Operations" or "Software Testpoint" => "M9,2 L9,7 M15,2 L15,7 M7,7 L17,7 L17,13 L14,16 L14,21 L10,21 L10,16 L7,13 Z",
        "Reboot to USB Update" or "UPDATE.APP Partition Extractor" => "M3,8 L3,21 L21,21 L21,8 M12,2 L12,15 M7,10 L12,15 L17,10",
        "Fastboot Flasher" or "Full OTA Flash" => "M3,6 L21,6 L21,18 L3,18 Z M16,12 L17,12 M19,12 L20,12",
        "Enable Downgrade" => "M5,9 A8,8 0 1 1 5,17 M5,3 L5,9 L11,9",
        "OEMInfo Converter" => "M12,2 L21,7 L21,17 L12,22 L3,17 L3,7 Z M8,12 L16,12 M12,8 L12,16",
        "Serial Number Management" => "M3,3 L13,3 L22,12 L12,22 L3,13 Z M7,7 A1,1 0 1 1 6.99,7",
        "Custom Version" => "M12,4 A8,8 0 1 1 11.99,4 M12,8 A4,4 0 1 1 11.99,8 M12,1 L12,4 M12,20 L12,23 M1,12 L4,12 M20,12 L23,12",
        "Preload Version" => "M3,16 L3,22 L21,22 L21,16 M12,2 L12,16 M7,11 L12,16 L17,11",
        "Base Version" => "M3,6 L12,2 L21,6 L21,18 L12,22 L3,18 Z M3,6 L12,10 L21,6 M12,10 L12,22",
        _ => "M12,2 A10,10 0 1 1 11.99,2 M12,10 L12,17 M12,6 L12,7"
    });
}

public class WindowsExpander : Expander
{
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<WindowsExpander, Geometry?>(nameof(Icon));
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HeaderProperty) SetCurrentValue(IconProperty, WindowsIcons.For(Header as string));
    }
}

public class WindowsCard : ContentControl
{
    public static readonly StyledProperty<string?> HeaderProperty =
        AvaloniaProperty.Register<WindowsCard, string?>(nameof(Header));
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<WindowsCard, Geometry?>(nameof(Icon));
    public string? Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HeaderProperty) SetCurrentValue(IconProperty, WindowsIcons.For(Header));
    }
}
