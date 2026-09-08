# NeverForget

NeverForget to przypominacz w architekturze klient-serwer:

- `NeverForget.Server` — ASP.NET Core Web API (.NET 8), lokalnie SQLite, na hostingu PostgreSQL;
- `NeverForget.Client` — aplikacja WPF (.NET 8/Windows);
- `NeverForget.Contracts` — współdzielone kontrakty REST;
- `NeverForget.Server.Tests` — testy integracyjne API.

Klient odpytuje serwer co 10 sekund o należne przypomnienia. Należne przypomnienie pojawia się na środku ekranu w oknie `Topmost`. Kliknięcie **OK** potwierdza je na serwerze, dzięki czemu nie zostanie pokazane ponownie.

## Uruchomienie lokalne

W pierwszym terminalu:

```powershell
dotnet run --project src/NeverForget.Server --launch-profile http
```

W drugim terminalu:

```powershell
dotnet run --project src/NeverForget.Client
```

Swagger jest dostępny pod `http://localhost:5081/swagger`. Baza SQLite `neverforget.db` powstaje automatycznie w katalogu roboczym serwera.

## Konfiguracja klienta

Ustawienia są w `src/NeverForget.Client/appsettings.json`:

```json
{
  "ServerUrl": "http://localhost:5081/",
  "ApiKey": "",
  "PollingIntervalSeconds": 10
}
```

Adres i klucz można też podać zmiennymi środowiskowymi `NEVERFORGET_API_URL` oraz `NEVERFORGET_API_KEY`. Adres serwera można zmienić bezpośrednio w głównym oknie klienta.

## Konfiguracja serwera

Bez dodatkowych ustawień serwer używa SQLite. Na hostingu ustaw:

- `ConnectionStrings__Postgres` — connection string PostgreSQL;
- `ApiKey` — długi, losowy sekret; tę samą wartość wpisz w konfiguracji klienta;
- `ASPNETCORE_ENVIRONMENT=Production`.

Endpoint `/health` nie wymaga klucza. Pozostałe endpointy wymagają nagłówka `X-Api-Key`, jeśli `ApiKey` został skonfigurowany na serwerze.

## REST API

- `POST /api/reminders` — dodanie;
- `PUT /api/reminders/{id}` — edycja i ponowne uzbrojenie;
- `DELETE /api/reminders/{id}` — usunięcie;
- `GET /api/reminders?from=...&to=...` — lista w okresie;
- `GET /api/reminders/due` — niepotwierdzone przypomnienia, których czas już nadszedł;
- `POST /api/reminders/{id}/acknowledge` — potwierdzenie;
- `GET /health` — health check.

## Testy i publikacja klienta

```powershell
dotnet test NeverForget.sln
dotnet publish src/NeverForget.Client -c Release -r win-x64 --self-contained false
```

WPF musi działać w tle, aby wyświetlać popupy. REST polling nie obudzi zamkniętej aplikacji Windows.

## Darmowy hosting MVP: Render + Neon

Najprostszy wariant bez opłat i bez karty płatniczej to:

1. Utwórz darmową bazę PostgreSQL w Neon i skopiuj connection string.
2. Umieść repozytorium w GitHubie lub GitLabie.
3. W Render utwórz **Web Service**, wybierz runtime **Docker**, plan **Free** i ten `Dockerfile`.
4. Ustaw health check na `/health`.
5. Dodaj zmienne `ConnectionStrings__Postgres`, `ApiKey` i `ASPNETCORE_ENVIRONMENT=Production`.
6. Po wdrożeniu wpisz adres `https://...onrender.com/` i ten sam klucz API w konfiguracji klienta.

Nie używaj darmowego Render Postgres do trwałych danych — obecnie wygasa po 30 dniach. Darmowy web service Render usypia po 15 minutach bez ruchu i może potrzebować około minuty na pierwszy start. Gdy klient WPF jest uruchomiony, regularne zapytania REST utrzymują usługę aktywną. Jest to dobre rozwiązanie dla MVP/hobby, ale nie zapewnia SLA ani gwarantowanych powiadomień co do sekundy.
