# Chattapplikation

En avancerad klient–server chattapplikation skriven i C# (.NET 10). Projektet innehåller separata projekt för `Server`, `Client` och gemensamma modeller i `Models`.

## Funktioner
- TCP-baserad chattserver som vidarebefordrar JSON-meddelanden till alla anslutna klienter.
- WinForms-klient med enkel UI för anslutning och skickande av textmeddelanden.
- Meddelandetyper: `TextMessage`, `PrivateMessage`, `SystemMessage`.
- Strukturerad JSON-loggning (Serilog) och per-client loggar.

## Förutsättningar
- .NET 10 SDK installerat
- Visual Studio 2022/2026 eller annan .NET-kompatibel IDE
- (Rekommenderat) Kör inte repo från OneDrive eller sökvägar med specialtecken som kan påverka Git/verktyg

## Projektstruktur
- `Server/Server.csproj` — serverapplikation (konsol)
- `Client/Client.csproj` — WinForms-klient
- `Models/Models.csproj` — delade meddelandeklasser
- `docs/diagrams/` — arkitekturdiagram (PlantUML)

## Bygga och köra (CLI)
Bygg och kör servern:

```powershell
dotnet build Server/Server.csproj
dotnet run --project Server/Server.csproj
```

Kör klienten i Visual Studio eller:

```powershell
dotnet run --project Client/Client.csproj
```

Standardporten är `5000`. Server hostas på `IPAddress.Any` och klienten ansluter som standard till `127.0.0.1:5000`.

## Konfiguration
- Starta servern med `--tls` för att försöka ladda `server.pfx` i applikationskatalogen. Om certifikat saknas startar den utan TLS.
- Max klienter konfigureras i `Server/Program.cs` (`MaxClients`).

## Utveckling för A‑nivå
Se `docs/` för förslag på designförbättringar, PlantUML-diagram och checklistor för att nå högsta betyg.
