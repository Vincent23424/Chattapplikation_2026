# Chattapplikation

En enkel klient–server chattapplikation skriven i C# (.NET 10). Projektet innehåller separata projekt för `Server`, `Client` och gemensamma modeller i `Models`.

## Funktioner
- TCP-baserad chattserver som vidarebefordrar JSON-meddelanden till alla anslutna klienter.
- WinForms-klient med enkel UI för anslutning och skickande av textmeddelanden.
- Meddelandetyper: `TextMessage`, `PrivateMessage`, `SystemMessage`.
- Loggning av inkommande meddelanden till `server_log.txt`.

## Förutsättningar
- .NET 10 SDK installerat
- Visual Studio 2022/2026 eller annan .NET-kompatibel IDE
- (Rekommenderat) Kör inte repo från OneDrive eller sökvägar med specialtecken som kan påverka Git/verktyg

## Projektstruktur
- `Server/Server.csproj` — serverapplikation (konsol)
- `Client/Client.csproj` — WinForms-klient
- `Models/Models.csproj` — delade meddelandeklasser

## Bygga och köra (CLI)
Bygg och kör servern:
