# GardeAudio

**Keep your headset selected when you plug in a PS5 DualSense controller.**

[![Build](https://github.com/Nagaraaa/GardeAudio/actions/workflows/build.yml/badge.svg)](https://github.com/Nagaraaa/GardeAudio/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![Platform: Windows x64](https://img.shields.io/badge/platform-Windows%20x64-0078D4.svg)](#download)

Windows can switch its default sound output to a controller when it is connected over USB. GardeAudio watches for a DualSense connection and restores the output you were using if that switch happens. It leaves the controller's audio device enabled.

This is a small, portable Windows utility. **v0.1.0 is an early preview, tested with one PS5 DualSense over USB on Windows 11.** The application interface is currently in French. [Guide en français](docs/README.fr.md).

## Download

| Package | Choose this if… |
| --- | --- |
| **[Windows x64 — standalone ZIP](https://github.com/Nagaraaa/GardeAudio/releases/download/v0.1.0/GardeAudio-v0.1.0-windows-x64.zip)** | You want to extract and run it. Includes the .NET runtime; no separate .NET installation needed. |
| [Windows x64 — small ZIP](https://github.com/Nagaraaa/GardeAudio/releases/download/v0.1.0/GardeAudio-v0.1.0-windows-x64-framework-dependent.zip) | You already have the [.NET 8 Desktop Runtime, x64](https://dotnet.microsoft.com/en-us/download/dotnet/8.0). |

[Release notes and SHA-256 checksums](https://github.com/Nagaraaa/GardeAudio/releases/tag/v0.1.0). These builds are unsigned.

## Get started

1. Extract the whole ZIP into a writable folder, then run `GardeAudio.exe`. Administrator rights are not required.
2. Select your headset in Windows before connecting the controller. Leave **Protéger les branchements de la DualSense** checked.
3. Pick a fallback output under **Sortie de secours**. It is only used if the previous output is unavailable.
4. Connect your DualSense over USB. GardeAudio watches for an unwanted switch during the next two seconds.
5. Use **Réduire près de l’horloge** to keep it running in the tray. **Quitter** and the window's close button both exit the program.

To switch outputs deliberately, select one under **Changer volontairement de sortie** and click **Utiliser**. This always takes precedence over a pending correction. You can also pause protection using the checkbox or tray menu.

There is no installer or automatic Windows startup entry. To uninstall, quit the application and delete its folder.

## What it does

- Remembers the previous default separately for Windows' console, multimedia and communications output roles.
- Arms protection only when a DualSense audio output becomes available, with at most one correction per role during a two-second window.
- Allows changes to a different headset or speakers during that window. Outside it, Windows output selection remains free, including the controller.
- Uses the previous available output, then a remembered valid output or your configured fallback. With no valid destination, it makes no forced selection.

It does not disable devices, install drivers, send controller input reports, change microphone defaults or adjust volume. Preferences and a local diagnostic log stay in `donnees/` beside the executable; the application has no telemetry or network calls.

## Limits to know

- **A manual choice of the controller in Windows during those first two seconds can be corrected too.** Windows does not report whether a default change was automatic or manual. Use **Utiliser** in GardeAudio, or pause protection, for this case.
- This restores a default after a notification; it does not prevent the driver's switch. A brief audio interruption can occur on other systems. Apps that explicitly select the controller instead of following the Windows default are not rerouted.
- Detection currently relies on the audio interface name containing `DualSense`. Other controllers, renamed interfaces, DualSense Edge and Bluetooth have not been validated.
- A controller already connected when GardeAudio starts is not treated as a new connection.
- **Haptics, adaptive triggers and game-specific controller speaker effects have not been tested in a compatible game.** The device stays enabled, but that is not proof that every game feature works.
- The output setter uses the undocumented Windows `PolicyConfig` COM interface. Windows updates may affect compatibility.

## Validation

| Check | Evidence for v0.1.0 |
| --- | --- |
| Policy tests | 25 automated scenarios covering reconnection, notification order, independent roles, manual choices, missing outputs, pause and loop prevention. |
| Windows integration | 11 checks on real audio endpoints, including a real default-output change and restoration. Controller arrival was simulated in the policy for these checks. |
| Physical USB reconnect | The tester unplugged and reconnected the DualSense while playing audio, and reported that sound stayed in the headset with no audible interruption. The log recorded a communications-role correction; multimedia already stayed on the headset. |
| Controller availability | A controller remained readable through WinMM after the tests. This does not validate every button or advanced game feature. |

See the [validation notes](docs/validation-v0.1.0.md) for scope and limitations. Automated CI runs the build and policy tests; it does not claim to test physical hardware.

## Build from source

Use Windows and the [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).

```powershell
dotnet build src/GardeAudio/GardeAudio.csproj -c Release
dotnet run --project tests/GardeAudio.Tests/GardeAudio.Tests.csproj -c Release
./scripts/Publish.ps1
```

The packaging script creates both ZIPs and `SHA256SUMS.txt` in `artifacts/`. The standalone build bundles .NET 8.0.32, including its license and notices. The app uses WinForms and Windows Core Audio COM interop, with no third-party NuGet libraries. The test project is a small console runner; a failed scenario exits with a nonzero code.

## Diagnostics and bug reports

For a read-only inventory:

```powershell
$process = Start-Process .\GardeAudio.exe -ArgumentList '--inventory inventory.json' -Wait -PassThru
$process.ExitCode
```

This writes device names, endpoint identifiers and default selections to `inventory.json`. Review and redact identifiers before sharing it. Report problems through [GitHub Issues](https://github.com/Nagaraaa/GardeAudio/issues), with your Windows version, controller model, connection type and what happened.

The developer-only `--live-test folder` command **changes real output defaults** before attempting to restore the originals. Its current fixture requires a Logitech PRO X headset, a DualSense and another output. Close the GUI before running it; the read-only inventory is sufficient for ordinary bug reports.

## License and references

[MIT](LICENSE). Bundled .NET components retain their own licenses in the standalone archive.

- [Windows audio endpoint notifications](https://learn.microsoft.com/en-us/windows/win32/api/mmdeviceapi/nn-mmdeviceapi-immnotificationclient)
- [Default output changes](https://learn.microsoft.com/en-us/windows/win32/api/mmdeviceapi/nf-mmdeviceapi-immnotificationclient-ondefaultdevicechanged)
- [Sony: using a DualSense on PC](https://www.playstation.com/en-us/support/hardware/pair-dualsense-controller-bluetooth/)
