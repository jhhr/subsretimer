# Icon

`subsretimer.ico` and `subsretimer-16.png` / `-32.png` / `-48.png` are the icon of the
original **Subs Re-Timer 1.0** by Christopher Brochtrup (a red *R*), taken unchanged from
`SubsReTimer/SubsReTimer/Art/` in the subs2srs 27.0 source archive
(`icon.ico`, `SubsRetimer16.png`, `SubsRetimer32.png`, `SubsRetimer48.png`) and renamed.

They are part of Subs Re-Timer and carry its licence: **GPL-3.0-or-later**, the same as this
port. See [LICENSE](../LICENSE).

Where they are used:

- `subsretimer.ico` is the executable's Win32 icon (`<ApplicationIcon>` in
  `SubsRetimer/SubsRetimer.csproj`, set only when this file exists).
- The PNGs are installed by `make install` into
  `$(PREFIX)/share/icons/hicolor/<size>x<size>/apps/subsretimer.png`, and copied into
  `share\icons\hicolor\...` inside the Windows bundle by `dist/windows/bundle-gtk.ps1`.
  The window asks for them by name (`Gtk.Window.SetIconName("subsretimer")`), which is also
  the `Icon=` of `dist/subsretimer.desktop`.
