# v0.1.0 validation notes

Validated on 20 October 2026. This is a record of checks on the development PC, not a general compatibility certification.

## Environment

- Windows 11 x64, build 26200.
- PS5 DualSense connected over USB.
- Logitech PRO X headset and an additional monitor audio output.
- .NET 8 application, built with SDK 8.0.424. Public standalone package bundles runtime 8.0.32.

## Automated policy checks: 25 passed

The console test runner covers arrival windows, independent audio roles, coupled-role acknowledgement, unchanged notifications, reconnection, unavailable previous outputs, fallback selection, no valid destination, paused protection, and manual choices. It exercises the same policy used by the desktop application, without requiring hardware.

## Local Windows integration: 11 passed

These checks ran against real Windows audio endpoints and callbacks:

1. Select the headset for console and multimedia.
2. Change a real Windows default to the controller and observe restoration after a policy-simulated arrival.
3. Respect a manual choice of another output during an arrival window.
4. Respect an explicit controller choice through the application's selection path.
5. Respect a Windows controller choice after the arrival window expires.
6. Stop correcting while paused.
7. Preserve audio endpoint states.
8. Preserve microphone defaults during the checks.
9. Keep the controller readable through WinMM.
10. Restore the original output defaults in cleanup.
11. Preserve the original input defaults after cleanup.

The USB arrival in this integration suite is simulated only inside the policy: no device is disabled or restarted. These checks are separate from the physical USB test below. They are not part of hosted CI and require the specific local hardware fixture.

## GUI and physical USB checks

The actual WinForms window was opened and inspected. Pause and resume were used through the interface.

With the guard active and audio playing, the tester physically unplugged and reconnected the DualSense. The tester confirmed that sound stayed in the headset, with no audible interruption. The local log recorded physical arrivals and a real correction of the communications output role to the headset. The multimedia output already stayed on the headset in this physical scenario.

This supports the observed result on that PC. It does not establish a universal interruption duration or prove that every game's output routing follows Windows defaults.

## Public package checks

Before publication, the source is rebuilt and policy tests are rerun. Both published executables are checked with the read-only inventory command, and the standalone GUI is checked for a responsive window. Release archives contain application files, usage instructions and licenses; personal settings and raw device inventories are excluded. Published downloads are checked against the supplied SHA-256 sums.

## Unverified

- Haptics, adaptive triggers, every physical button and game-specific speaker effects: no compatible game was available.
- DualSense Edge, other controller families, Bluetooth, Windows ARM64 and other PCs.
- Windows 10 compatibility: the target framework does not substitute for a real test on that OS.

The application keeps audio endpoints enabled and does not send HID reports. Those implementation properties alone do not prove complete controller feature compatibility.

Raw inventories and journals are not included in this repository because they contain device identifiers. [Build and reproduce the policy checks](../README.md#build-from-source).
