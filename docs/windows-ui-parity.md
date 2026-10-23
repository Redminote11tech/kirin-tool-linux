# Windows presentation on Linux

Version 2.4.6 recreates the presentation of the frozen Windows v2.4.2 source
(commit 45440d47296ef4c250fc03730259dbeebb245ebc). That version uses WPF-UI;
the Linux port uses Avalonia. Newer upstream GUI source was not imported, and
no Windows executable was run under Wine for this work. Source and attribution
remain under the existing BSL 1.1 terms; this is not an open-source license.

## Matching elements

- 1000 × 700 main content, a 48-pixel application title bar and 320-pixel sidebar.
  The main window uses border-only decorations to avoid a second desktop title
  bar. Caption buttons minimize, maximize/restore and close the actual window;
  the title supports dragging and double-click maximize/restore.
- Nine navigation entries, with outline icons, 40-pixel rows, hover feedback and
  a rounded selected row with an accent indicator. Extractor Tools is retained.
- Common expandable cards with an icon, header, chevron, border and padded body.
  Toggle buttons preserve keyboard focus and activation as well as pointer use.
- Device information is presented in eight icon-bearing label/value cards,
  retaining the named controls used by the existing read handlers. Long values
  wrap. Primary/caution actions recover their frozen-source visual roles.
- Modal content windows have consistent border-only decoration and owner-centered
  placement. Existing ownership, close/cancellation rules and operation callbacks
  remain in place. Progress statuses wrap and retain full error text in tooltips.

## Deliberate platform differences

Inter remains bundled; proprietary Segoe UI and Windows icon fonts are not copied.
Outline geometries are independently drawn. Typography and rasterization therefore
cannot be claimed pixel-identical to a Windows installation. Windows Mica/compositor
backdrops are represented by the existing solid light surfaces. The Linux title
retains the port's identity; chipset notices and Linux-only tools remain visible.

File/folder pickers use Avalonia's platform storage provider, so their appearance
follows the desktop portal or operating system. System window borders, scale
factors and window-management behavior can vary by compositor. The custom caption
needs normal desktop validation on the user's window manager; headless interaction
checks do not establish every Wayland/X11 compositor behavior.

## Validation

`dotnet run --project tests/VisualParity -c Release -- /tmp/kirin-ui-previews`
checks page routing, visible navigation sizes, actual pointer expansion, information
fields and modal confirmation/notice callbacks. It writes Skia-rendered screenshots
of the actual Avalonia controls, not HTML mockups. Existing extractor, Kirin 9000,
udev notice, VCOM prompt, USB Update UI, OEMInfo and stable-boundary harnesses remain
part of verification. Tests use fixtures/stubs and do not operate a physical phone.

The native fastboot engine and its validation limits remain those documented in
[native-fastboot-parity-v2.4.5.md](native-fastboot-parity-v2.4.5.md).
