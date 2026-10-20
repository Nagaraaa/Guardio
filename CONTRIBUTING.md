# Contributing

Small, reproducible fixes and hardware compatibility reports are welcome. For larger features, open an issue first so the intended behavior can be discussed.

Use Windows with the .NET 8 SDK. Before submitting a code change, run:

```powershell
dotnet build src/Guardio/Guardio.csproj -c Release
dotnet run --project tests/Guardio.Tests/Guardio.Tests.csproj -c Release
```

Changes to the policy should preserve manual choices, the short arrival window and the rule that unavailable outputs are never selected. Add a focused regression scenario for a behavior change. Do not turn the guard into a permanent default-output lock.

Keep simulated checks and real hardware results distinct. Do not describe a successful build or a readable controller as proof of haptics or adaptive triggers. The diagnostic `--live-test` command changes actual audio defaults and currently depends on a specific headset fixture; it is not required for every contributor.

Do not commit local `donnees/` folders, raw inventories or device identifiers. Include the controller model, connection type and Windows version in compatibility reports.
