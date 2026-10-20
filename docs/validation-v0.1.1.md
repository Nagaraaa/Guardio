# v0.1.1 validation notes

21 October 2026. GardeAudio is now named **Guardio**.

## Scope

This release renames the repository, project paths, namespaces, executable, window and tray labels, documentation, and downloadable archives. The audio policy is unchanged apart from its namespace. The diagnostics and audio service retain their behavior; the version reported in status is now 0.1.1.

Preferences still use the same `donnees/` folder and JSON layout. The original instance mutex is deliberately retained so an older GardeAudio copy and Guardio cannot both control audio defaults. Version 0.1.0 remains available under its original tag and filenames.

If a renamed package fails validation, publication stops. The previous tagged source and release remain recoverable. To return to the previous application, quit Guardio and run the original v0.1.0 executable with the same `donnees/` folder. There is no settings migration to reverse.

## Checks for this release

- Build the renamed application in Release configuration and rerun all 25 policy scenarios.
- Produce fresh standalone and framework-dependent packages with `Guardio.exe`, usage instructions and licenses only.
- Run the read-only inventory from each extracted package and check that Windows outputs and the controller remain accessible.
- Open the standalone GUI, check the Guardio window title, nonzero window handle and responsiveness, then close that test instance normally.
- Load an owned settings fixture in the existing JSON format and confirm that the fallback and paused state are retained. Compare read-only inventories before and after the GUI check to confirm that input and output defaults stay unchanged.
- Run the Windows build/test/package workflow against the release commit.
- Verify anonymous access to the new release downloads and compare their SHA-256 hashes with local packages.

## Hardware evidence

The 11 native integration checks and physical USB reconnection evidence belong to [v0.1.0](validation-v0.1.0.md). They were not repeated as part of a naming-only change. The policy source is compared with that tag to confirm that its only change is the namespace.

No new claim is made for haptics, adaptive triggers, other controller families or Bluetooth. These remain unverified in a compatible game or supported hardware scenario.
