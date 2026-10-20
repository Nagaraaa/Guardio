# Contributing

Fixes and controller compatibility reports are welcome. For larger features, open an issue first.

Before submitting a change, on Windows with the .NET 8 SDK:

```powershell
dotnet build src/Guardio/Guardio.csproj -c Release
dotnet run --project tests/Guardio.Tests/Guardio.Tests.csproj -c Release
```

Policy changes should keep manual choices free, keep the short arrival window, and never select an unavailable output. Add a test scenario for any behavior change.

When adding controller detection, add the name to `ControllerDetector` and a test. Don't commit `donnees/` folders, inventories or device IDs.
