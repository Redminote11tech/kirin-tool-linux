// Licensed under the Business Source License 1.1. See LICENSE.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Kirin_Tool.UI;

// Original Fluent System Icons glyphs; font copyright/license in Assets/Fonts.
public class WindowsSymbol : TextBlock
{
    public static readonly StyledProperty<string?> SymbolProperty = AvaloniaProperty.Register<WindowsSymbol, string?>(nameof(Symbol));
    public static readonly StyledProperty<bool> FilledProperty = AvaloniaProperty.Register<WindowsSymbol, bool>(nameof(Filled));
    public string? Symbol { get => GetValue(SymbolProperty); set => SetValue(SymbolProperty, value); }
    public bool Filled { get => GetValue(FilledProperty); set => SetValue(FilledProperty, value); }
    public WindowsSymbol() { FontSize = 20; UpdateGlyph(); }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SymbolProperty || change.Property == FilledProperty) UpdateGlyph();
    }
    private void UpdateGlyph()
    {
        FontFamily = new FontFamily("avares://Kirin-Tool/Assets/Fonts#FluentSystemIcons-" + (Filled ? "Filled" : "Regular"));
        Text = char.ConvertFromUtf32(Codepoint(Symbol, Filled));
    }
    private static int Codepoint(string? symbol, bool filled) => symbol switch
    {
        "ArrowDownload24" => filled ? 61777 : 61777,
        "ArrowSwap24" => filled ? 61838 : 61838,
        "ArrowSync24" => filled ? 61841 : 61841,
        "ArrowUpload24" => filled ? 61861 : 61861,
        "Book24" => filled ? 57715 : 63742,
        "Box24" => filled ? 57808 : 57788,
        "Checkmark24" => filled ? 62101 : 62101,
        "CloudSync24" => filled ? 58308 : 58287,
        "Code24" => filled ? 62192 : 62192,
        "Delete16" => filled ? 58503 : 58491,
        "Dismiss24" => filled ? 62314 : 62314,
        "Edit16" => filled ? 62427 : 62428,
        "ErrorCircle24" => filled ? 62449 : 62450,
        "Eye24" => filled ? 58880 : 58867,
        "FolderOpen16" => filled ? 62513 : 62509,
        "FolderOpen24" => filled ? 62515 : 62511,
        "Globe24" => filled ? 62559 : 62555,
        "Home12" => filled ? 59163 : 59150,
        "Home24" => filled ? 62600 : 62593,
        "Info24" => filled ? 62635 : 62628,
        "LockOpen24" => filled ? 59300 : 59286,
        "LockShield24" => filled ? 62733 : 62723,
        "NumberSymbol24" => filled ? 62856 : 62846,
        "PersonLock24" => filled ? 59720 : 59702,
        "Phone24" => filled ? 62955 : 62945,
        "PhoneLaptop24" => filled ? 62965 : 62955,
        "Power24" => filled ? 63001 : 62991,
        "Save24" => filled ? 63114 : 63104,
        "Search24" => filled ? 63130 : 63120,
        "Send24" => filled ? 63139 : 63130,
        "Settings24" => filled ? 63155 : 63146,
        "Shield24" => filled ? 63176 : 63167,
        "ShieldQuestion24" => filled ? 61219 : 61219,
        "Storage24" => filled ? 63300 : 63276,
        "Tag24" => filled ? 63381 : 63357,
        "UsbPlug24" => filled ? 60880 : 60880,
        _ => filled ? 62635 : 62628
    };
}

public class WindowsButton : Button
{
    public static readonly StyledProperty<string?> SymbolProperty = AvaloniaProperty.Register<WindowsButton, string?>(nameof(Symbol));
    public string? Symbol { get => GetValue(SymbolProperty); set => SetValue(SymbolProperty, value); }
}
