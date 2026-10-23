# Windows presentation on Linux

Version 2.4.7 recreates the presentation of the frozen Windows v2.4.2 source
(commit 45440d47296ef4c250fc03730259dbeebb245ebc). That version uses WPF-UI;
the Linux port uses Avalonia. Newer upstream GUI source was not imported. ILSpy recovered the frozen binary’s
Fluent icon resources and InfoBar layout. An offline WPF harness rendered the
original assembly under Wine for page and partial-OTA comparisons; Wine is only
a development reference, and is not required to run the Linux application. Source and attribution
remain under the existing BSL 1.1 terms; this is not an open-source license.

## Matching elements

- 1000 × 700 main content, a 48-pixel application title bar and 320-pixel sidebar.
  The main window uses border-only decorations to avoid a second desktop title
  bar. Caption buttons minimize, maximize/restore and close the actual window;
  the title supports dragging and double-click maximize/restore.
- The title includes the assembly version and `Linux port by Redminote11tech`.
- Nine navigation entries, with the original Fluent outline icons, 40-pixel rows, hover feedback and
  a rounded selected row with an accent indicator. Extractor Tools is retained.
- Common expandable cards with an icon, header, chevron, border and padded body.
  Toggle buttons preserve keyboard focus and activation as well as pointer use.
- Device information is presented in eight icon-bearing label/value cards,
  retaining the named controls used by the existing read handlers. Long values
  wrap. Primary/caution actions recover their frozen-source visual roles.
- Modal content windows have consistent border-only decoration and owner-centered
  placement. Existing ownership, close/cancellation rules and operation callbacks
  remain in place. Progress statuses wrap and retain full error text in tooltips.

## Original Fluent resources and warnings

Regular and Filled Fluent System Icons fonts are bundled with their Microsoft MIT
license in `Assets/Fonts/LICENSE.txt` and the published licenses directory. Icons
do not depend on a Windows font installation. Navigation, information cards,
expanders and the corresponding original actions use the recovered glyphs.

The incomplete-OTA panel matches the original warning icon, yellow surface,
16 × 12 padding, title/message flow and dismiss button. Changing selected files
reopens a dismissed warning. Empty and complete selections hide it; fastboot and
USB Update use four and six files respectively. The OTA action is a primary blue
button with the Cloud Sync icon and `Start` label. Persistent Linux udev and Kirin
9000 notices retain their existing behavior.

## Deliberate platform differences

Inter remains bundled; proprietary Segoe UI is not redistributed. The MIT-licensed
Fluent icon fonts are bundled. Text typography and rasterization therefore
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
fields, version/credit, icon-bearing label fit, warning dismissal/reopening, partial
and complete four/six-file OTA states, and modal confirmation/notice callbacks. It writes Skia-rendered screenshots
of the actual Avalonia controls, not HTML mockups. Existing extractor, Kirin 9000,
udev notice, VCOM prompt, USB Update UI, OEMInfo and stable-boundary harnesses remain
part of verification. Tests use fixtures/stubs and do not operate a physical phone.

The native fastboot engine and its validation limits remain those documented in
[native-fastboot-parity-v2.4.5.md](native-fastboot-parity-v2.4.5.md).
