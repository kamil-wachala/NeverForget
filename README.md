# NeverForget

NeverForget is a .NET 8 client/server desktop application for multiple Google calendars:

- `NeverForget.Server` exposes a REST API and communicates with Google Calendar;
- `NeverForget.Client` is a WPF calendar browser/editor and notification client;
- `NeverForget.Contracts` contains the shared REST contracts;
- `NeverForget.Server.Tests` verifies the REST API using a fake Google service.

The previous local reminder database and cron scheduler have been removed. Google Calendar is now the source of truth.

## Features

- select any combination of configured Google calendars;
- list events between two dates, including instances of recurring Google events;
- add timed or all-day events to writable calendars;
- edit existing event title, description, location, dates, times, and notification lead time;
- poll the server and show due events in a centered, topmost popup;
- mark calendars as read-only in NeverForget while still displaying their events.

The WPF client must be running to display its own popups. Google events remain available in Google Calendar independently of NeverForget.

## Google service-account setup

This version uses one Google service account. It is a good fit for a personal/single-user deployment because the server can run unattended and does not store a user's refresh token.

1. Create or select a project in Google Cloud Console.
2. Enable the **Google Calendar API**.
3. Create a service account and download a JSON key.
4. In Google Calendar, open each calendar's settings and share it with the service account email.
5. Grant **Make changes to events** for calendars NeverForget may edit, or a read-only permission for calendars it should only display.
6. Copy each calendar ID from **Settings and sharing > Integrate calendar > Calendar ID**.

Service accounts are application identities, not normal Google users. A calendar must be explicitly shared with the service account and included in the server configuration. Google Workspace administrators can alternatively configure domain-wide delegation, but user impersonation is not implemented in this version.

For local development, save the downloaded key as:

```text
src/NeverForget.Server/google-service-account.json
```

The filename is ignored by Git. Never commit a service-account private key.

Then update `src/NeverForget.Server/appsettings.json`:

```json
{
  "GoogleCalendar": {
    "ServiceAccountCredentialPath": "google-service-account.json",
    "ServiceAccountCredentialJson": "",
    "CalendarIds": [
      "your-address@gmail.com",
      "project-id@group.calendar.google.com"
    ],
    "ReadOnlyCalendarIds": [
      "holidays-id@group.calendar.google.com"
    ],
    "DefaultNotificationMinutesBefore": 10
  }
}
```

`DefaultNotificationMinutesBefore` is used for existing Google events that do not have an explicit popup reminder. Events created or edited by NeverForget receive an explicit popup reminder value.

## Running locally

In the first terminal:

```powershell
dotnet run --project src/NeverForget.Server --launch-profile http
```

In the second terminal:

```powershell
dotnet run --project src/NeverForget.Client
```

Swagger is available at `http://localhost:5081/swagger`.

## Production configuration

Set these environment variables on the host:

- `GoogleCalendar__ServiceAccountCredentialJson` - the complete service-account JSON key;
- `GoogleCalendar__CalendarIds__0`, `GoogleCalendar__CalendarIds__1`, etc.;
- `GoogleCalendar__ReadOnlyCalendarIds__0`, etc., when applicable;
- `GoogleCalendar__DefaultNotificationMinutesBefore`;
- `ApiKey` - a long random secret shared with the client;
- `ASPNETCORE_ENVIRONMENT=Production`.

Do not put the credential JSON in a public container image or source repository. Use the hosting provider's secret/environment-variable facility.

## Client configuration

Settings are stored in `src/NeverForget.Client/appsettings.json`:

```json
{
  "ServerUrl": "http://localhost:5081/",
  "ApiKey": "",
  "PollingIntervalSeconds": 10
}
```

The server address and API key can also be supplied through `NEVERFORGET_API_URL` and `NEVERFORGET_API_KEY`.

## REST API

- `GET /api/calendars` - list configured Google calendars;
- `GET /api/calendar-events?from=...&to=...&calendarIds=...` - list events;
- `GET /api/calendar-events/{eventId}?calendarId=...` - get one event;
- `GET /api/calendar-events/notifications?from=...&to=...&calendarIds=...` - list due notifications;
- `POST /api/calendar-events` - create an event;
- `PUT /api/calendar-events/{eventId}` - update an event;
- `GET /health` - health check.

When `ApiKey` is configured, all endpoints except `/health` require the `X-Api-Key` header.

## Build and test

```powershell
dotnet build NeverForget.sln
dotnet test NeverForget.sln
dotnet publish src/NeverForget.Client -c Release -r win-x64 --self-contained false
```

## Hoppscotch

Import `http/NeverForget.hoppscotch.json`. Set its `calendarId` variable to a configured Google calendar ID and copy an event ID returned by create/list into `eventId` before calling get or update.
