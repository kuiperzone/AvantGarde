# RELEASE NOTES
See Assets for installation files, which include multiple options for Linux and Setup for Windows. The project can easily be built from source for Mac.

```
+ 1.6.0;2025-07-06
- Change: Built against Avalonia .NET 9 / 11.3.2.
- Added: Preview copy to clipboard.
- Added: Checkered tile background.
- Change: AppImage built against recent appimagetool which should resolve fuse2/3 issues.
- Change: Subtle changes to theme colors.
- Change: Child windows no longer show in taskbar.
- Fix: App icon now displays.
- Change: Minor changes and code cleanup.
```

## APPIMAGE
AppImage is simple on Linux. Just download and run, but don't forget to set the 'Execute Permission' flag.

## DEB & RPM
To install on Linux, download and install from local file:

    sudo apt install ./avantgarde_1.6.0-1_amd64.deb

or

    sudo dnf install ./avantgarde_1.6.0-1.x86_64.rpm

Note the leading ./ prefix.

For deb and dnf, you may have to uninstall it from the command line as the Software Center may not support removal of packages installed as local files.

    sudo apt remove avantgarde
    sudo dnf remove avantgarde


## Footnotes
I've include builds for ARM64 this time, but I haven't been able to test these. Would be nice to know if they work.

Unfortunately I wasn't able to get Avant Garde to run properly under flatpak due to sandboxing.
