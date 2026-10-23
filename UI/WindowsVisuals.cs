// Licensed under the Business Source License 1.1. See LICENSE.
using Avalonia;
using Avalonia.Controls;

namespace Kirin_Tool.UI;

public static class WindowsIcons
{
    public static string For(string? name) => name switch
    {
        "Quick Start Guide" => "Home12",
        "Information" => "Info24",
        "FB Lock" => "LockShield24",
        "User Lock" => "PersonLock24",
        "Device Model" => "Phone24",
        "Vendor / Country" => "Globe24",
        "Build Number" => "NumberSymbol24",
        "Base Version" => "Box24",
        "Custom Version" => "Settings24",
        "Preload Version" => "ArrowDownload24",
        "Device Operations" => "UsbPlug24",
        "Software Testpoint" => "UsbPlug24",
        "Full OTA Flash" => "CloudSync24",
        "Enable Downgrade" => "ArrowDownload24",
        "Reboot to USB Update" => "ArrowSync24",
        "Fastboot Flasher" => "PhoneLaptop24",
        "Security Bypass" => "Shield24",
        "Unlocking" => "LockOpen24",
        "OEMInfo Converter" => "ArrowSwap24",
        "Serial Number Management" => "Tag24",
        "About and Credits" => "Info24",
        "UPDATE.APP Partition Extractor" => "ArrowDownload24",
        _ => "Info24"
    };
}

public class WindowsExpander : Expander
{
    public static readonly StyledProperty<string?> IconProperty =
        AvaloniaProperty.Register<WindowsExpander, string?>(nameof(Icon));
    public string? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
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
    public static readonly StyledProperty<string?> IconProperty =
        AvaloniaProperty.Register<WindowsCard, string?>(nameof(Icon));
    public string? Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
    public string? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HeaderProperty) SetCurrentValue(IconProperty, WindowsIcons.For(Header));
    }
}
