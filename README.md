# NeverForget

NeverForget is a reminder application built using a client-server architecture:

- `NeverForget.Server` — ASP.NET Core Web API (.NET 8), using SQLite locally and PostgreSQL when hosted;
- `NeverForget.Client` — WPF application (.NET 8/Windows);
- `NeverForget.Contracts` — shared REST contracts;
- `NeverForget.Server.Tests` — API integration tests.

The client polls the server every 10 seconds for due reminders. A due reminder appears in a centered `Topmost` window. Clicking **OK** acknowledges it on the server so it will not be displayed again.

## Running locally

In the first terminal:

```powershell
dotnet run --project src/NeverForget.Server --launch-profile http
```

In the second terminal:

```powershell
dotnet run --project src/NeverForget.Client
```

Swagger is available at `http://localhost:5081/swagger`. The `neverforget.db` SQLite database is created automatically in the server's working directory.

## Client configuration

Settings are stored in `src/NeverForget.Client/appsettings.json`:

```json
{
  "ServerUrl": "http://localhost:5081/",
  "ApiKey": "",
  "PollingIntervalSeconds": 10
}
```

The server address and API key can also be supplied through the `NEVERFORGET_API_URL` and `NEVERFORGET_API_KEY` environment variables. The server address can be changed directly in the client's main window.

## Server configuration

The server uses SQLite when no additional configuration is provided. When hosting it, set:

- `ConnectionStrings__Postgres` — the PostgreSQL connection string;
- `ApiKey` — a long, randomly generated secret; configure the client with the same value;
- `ASPNETCORE_ENVIRONMENT=Production`.

The `/health` endpoint does not require a key. All other endpoints require the `X-Api-Key` header when `ApiKey` is configured on the server.

## REST API

- `POST /api/reminders` — create a reminder;
- `PUT /api/reminders/{id}` — update and rearm a reminder;
- `DELETE /api/reminders/{id}` — delete a reminder;
- `GET /api/reminders?from=...&to=...` — list reminders within a time range;
- `GET /api/reminders/due` — list unacknowledged reminders that are due;
- `POST /api/reminders/{id}/acknowledge` — acknowledge a reminder;
- `GET /health` — health check.

## Tests and client publishing

```powershell
dotnet test NeverForget.sln
dotnet publish src/NeverForget.Client -c Release -r win-x64 --self-contained false
```

The WPF application must remain running to display reminder popups. REST polling cannot wake a closed Windows application.

## Free MVP hosting: Render + Neon

The simplest free setup without requiring a payment card is:

1. Create a free PostgreSQL database in Neon and copy its connection string.
2. Push the repository to GitHub or GitLab.
3. Create a **Web Service** in Render, select the **Docker** runtime, the **Free** plan, and this repository's `Dockerfile`.
4. Set the health-check path to `/health`.
5. Add `ConnectionStrings__Postgres`, `ApiKey`, and `ASPNETCORE_ENVIRONMENT=Production` as environment variables.
6. After deployment, configure the client with the `https://...onrender.com/` address and the same API key.

Do not use free Render Postgres for persistent data because it currently expires after 30 days. A free Render web service spins down after 15 minutes without traffic and may need about a minute to handle the first request after that. While the WPF client is running, its regular REST requests keep the service active. This setup is suitable for an MVP or hobby project, but it does not provide an SLA or second-level reminder delivery guarantees.
