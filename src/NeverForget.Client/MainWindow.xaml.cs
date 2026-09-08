using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using NeverForget.Contracts;

namespace NeverForget.Client;

public partial class MainWindow : Window
{
    private static readonly string[] TimeFormats = ["h\\:mm", "hh\\:mm"];
    private readonly ClientSettings _settings = ClientSettings.Load();
    private readonly DispatcherTimer _pollingTimer;
    private readonly HashSet<string> _displayedNotifications = new(StringComparer.Ordinal);
    private GoogleCalendarApiClient? _apiClient;
    private string? _currentServerUrl;
    private string? _editedEventId;
    private DateTimeOffset _lastNotificationCheckUtc;
    private bool _isPolling;

    public ObservableCollection<CalendarListItem> Calendars { get; } = [];
    public ObservableCollection<CalendarListItem> WritableCalendars { get; } = [];
    public ObservableCollection<CalendarEventListItem> Events { get; } = [];

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        ServerUrlTextBox.Text = _settings.ServerUrl;
        FromDatePicker.SelectedDate = DateTime.Today;
        ToDatePicker.SelectedDate = DateTime.Today.AddDays(14);
        ResetEditor();
        _lastNotificationCheckUtc = DateTimeOffset.UtcNow.AddMinutes(-1);

        _pollingTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(_settings.PollingIntervalSeconds)
        };
        _pollingTimer.Tick += PollingTimer_Tick;

        Loaded += async (_, _) =>
        {
            if (await LoadCalendarsAsync())
            {
                await RefreshEventsAsync();
                await PollNotificationsAsync();
            }

            _pollingTimer.Start();
        };
        Closed += (_, _) => _apiClient?.Dispose();
    }

    private GoogleCalendarApiClient GetApiClient()
    {
        var serverUrl = ServerUrlTextBox.Text.Trim();
        if (!serverUrl.EndsWith('/'))
        {
            serverUrl += '/';
        }

        if (_apiClient is not null
            && string.Equals(_currentServerUrl, serverUrl, StringComparison.OrdinalIgnoreCase))
        {
            return _apiClient;
        }

        _apiClient?.Dispose();
        _apiClient = new GoogleCalendarApiClient(serverUrl, _settings.ApiKey);
        _currentServerUrl = serverUrl;
        return _apiClient;
    }

    private async void ReloadCalendarsButton_Click(object sender, RoutedEventArgs e)
    {
        if (await LoadCalendarsAsync())
        {
            await RefreshEventsAsync();
        }
    }

    private async Task<bool> LoadCalendarsAsync()
    {
        var loaded = false;
        await RunUiActionAsync(async () =>
        {
            var selectedIds = Calendars
                .Where(x => x.IsSelected)
                .Select(x => x.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var calendars = await GetApiClient().GetCalendarsAsync();

            Calendars.Clear();
            WritableCalendars.Clear();
            foreach (var calendar in calendars)
            {
                var item = new CalendarListItem(calendar)
                {
                    IsSelected = selectedIds.Count == 0 || selectedIds.Contains(calendar.Id)
                };
                Calendars.Add(item);
                if (item.CanWrite)
                {
                    WritableCalendars.Add(item);
                }
            }

            CalendarComboBox.SelectedIndex = WritableCalendars.Count > 0 ? 0 : -1;
            StatusTextBlock.Text = $"Loaded {Calendars.Count} Google calendars.";
            loaded = true;
        });
        return loaded;
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) =>
        await RefreshEventsAsync();

    private async Task RefreshEventsAsync()
    {
        if (FromDatePicker.SelectedDate is not DateTime fromDate
            || ToDatePicker.SelectedDate is not DateTime toDate)
        {
            ShowError("Select both a start date and an end date.");
            return;
        }

        if (fromDate.Date > toDate.Date)
        {
            ShowError("The start date cannot be later than the end date.");
            return;
        }

        var calendarIds = GetSelectedCalendarIds();
        if (calendarIds.Count == 0)
        {
            ShowError("Select at least one calendar.");
            return;
        }

        await RunUiActionAsync(async () =>
        {
            var from = ToLocalDateTimeOffset(fromDate.Date);
            var to = ToLocalDateTimeOffset(toDate.Date.AddDays(1));
            var events = await GetApiClient().GetEventsAsync(calendarIds, from, to);

            Events.Clear();
            foreach (var calendarEvent in events)
            {
                Events.Add(new CalendarEventListItem(calendarEvent));
            }

            StatusTextBlock.Text = $"Loaded {Events.Count} events. Last updated: {DateTime.Now:T}";
        });
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadEditor(out var input))
        {
            return;
        }

        await RunUiActionAsync(async () =>
        {
            if (_editedEventId is not null)
            {
                await GetApiClient().UpdateEventAsync(
                    _editedEventId,
                    new UpdateCalendarEventRequest(
                        input.CalendarId,
                        input.Title,
                        input.Description,
                        input.Location,
                        input.StartsAt,
                        input.EndsAt,
                        input.IsAllDay,
                        input.NotificationMinutesBefore));
                StatusTextBlock.Text = "The Google Calendar event was updated.";
            }
            else
            {
                await GetApiClient().CreateEventAsync(
                    new CreateCalendarEventRequest(
                        input.CalendarId,
                        input.Title,
                        input.Description,
                        input.Location,
                        input.StartsAt,
                        input.EndsAt,
                        input.IsAllDay,
                        input.NotificationMinutesBefore));
                StatusTextBlock.Text = "The Google Calendar event was added.";
            }

            ResetEditor();
            await RefreshEventsAsync();
        });
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        EventsGrid.SelectedItem = null;
        ResetEditor();
    }

    private void EventsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EventsGrid.SelectedItem is not CalendarEventListItem selected)
        {
            return;
        }

        var calendar = Calendars.FirstOrDefault(x =>
            string.Equals(x.Id, selected.CalendarId, StringComparison.OrdinalIgnoreCase));
        _editedEventId = selected.Id;
        EditorHeading.Text = calendar?.CanWrite == true ? "Edit event" : "View event (read-only)";
        SaveButton.Content = "Save event";
        SaveButton.IsEnabled = calendar?.CanWrite == true;
        CalendarComboBox.IsEnabled = false;
        CalendarComboBox.SelectedItem = calendar?.CanWrite == true ? calendar : null;

        var calendarEvent = selected.Event;
        TitleTextBox.Text = calendarEvent.Title;
        DescriptionTextBox.Text = calendarEvent.Description ?? string.Empty;
        LocationTextBox.Text = calendarEvent.Location ?? string.Empty;
        NotificationMinutesTextBox.Text = calendarEvent.NotificationMinutesBefore
            .ToString(CultureInfo.InvariantCulture);
        AllDayCheckBox.IsChecked = calendarEvent.IsAllDay;

        var start = calendarEvent.StartsAt.LocalDateTime;
        var end = calendarEvent.EndsAt.LocalDateTime;
        StartDatePicker.SelectedDate = start.Date;
        EndDatePicker.SelectedDate = calendarEvent.IsAllDay ? end.Date.AddDays(-1) : end.Date;
        StartTimeTextBox.Text = start.ToString("HH:mm", CultureInfo.InvariantCulture);
        EndTimeTextBox.Text = end.ToString("HH:mm", CultureInfo.InvariantCulture);
    }

    private async void PollingTimer_Tick(object? sender, EventArgs e) =>
        await PollNotificationsAsync();

    private async Task PollNotificationsAsync()
    {
        if (_isPolling)
        {
            return;
        }

        var calendarIds = GetSelectedCalendarIds();
        if (calendarIds.Count == 0)
        {
            return;
        }

        _isPolling = true;
        var checkedAt = DateTimeOffset.UtcNow;
        try
        {
            var notifications = await GetApiClient().GetNotificationsAsync(
                calendarIds,
                _lastNotificationCheckUtc,
                checkedAt);
            _lastNotificationCheckUtc = checkedAt;

            foreach (var notification in notifications)
            {
                var key = $"{notification.Event.CalendarId}|{notification.Event.Id}|{notification.Event.StartsAt:O}";
                if (!_displayedNotifications.Add(key))
                {
                    continue;
                }

                new CalendarNotificationPopup(notification) { Owner = this }.ShowDialog();
            }

            StatusTextBlock.Text = $"Connected. Last notification check: {DateTime.Now:T}";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or ArgumentException)
        {
            StatusTextBlock.Text = $"Cannot check Google Calendar: {ex.Message}";
        }
        finally
        {
            _isPolling = false;
        }
    }

    private bool TryReadEditor(out EventEditorInput input)
    {
        input = default!;
        if (CalendarComboBox.SelectedItem is not CalendarListItem calendar)
        {
            ShowError("Select a writable calendar.");
            return false;
        }

        var title = TitleTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            ShowError("The event title is required.");
            return false;
        }

        if (!int.TryParse(NotificationMinutesTextBox.Text, out var notificationMinutes)
            || notificationMinutes is < 0 or > 40320)
        {
            ShowError("Notification time must be between 0 and 40320 minutes.");
            return false;
        }

        if (StartDatePicker.SelectedDate is not DateTime startDate
            || EndDatePicker.SelectedDate is not DateTime endDate)
        {
            ShowError("Select both a start date and an end date.");
            return false;
        }

        var isAllDay = AllDayCheckBox.IsChecked == true;
        DateTimeOffset startsAt;
        DateTimeOffset endsAt;
        if (isAllDay)
        {
            startsAt = ToLocalDateTimeOffset(startDate.Date);
            endsAt = ToLocalDateTimeOffset(endDate.Date.AddDays(1));
        }
        else
        {
            if (!TryReadTime(StartTimeTextBox.Text, out var startTime)
                || !TryReadTime(EndTimeTextBox.Text, out var endTime))
            {
                ShowError("Enter valid start and end times in HH:mm format.");
                return false;
            }

            startsAt = ToLocalDateTimeOffset(startDate.Date.Add(startTime));
            endsAt = ToLocalDateTimeOffset(endDate.Date.Add(endTime));
        }

        if (endsAt <= startsAt)
        {
            ShowError("The event end must be later than its start.");
            return false;
        }

        input = new EventEditorInput(
            calendar.Id,
            title,
            NullIfWhiteSpace(DescriptionTextBox.Text),
            NullIfWhiteSpace(LocationTextBox.Text),
            startsAt,
            endsAt,
            isAllDay,
            notificationMinutes);
        return true;
    }

    private void ResetEditor()
    {
        _editedEventId = null;
        EditorHeading.Text = "New event";
        SaveButton.Content = "Add event";
        SaveButton.IsEnabled = true;
        CalendarComboBox.IsEnabled = true;
        CalendarComboBox.SelectedIndex = WritableCalendars.Count > 0 ? 0 : -1;
        TitleTextBox.Clear();
        DescriptionTextBox.Clear();
        LocationTextBox.Clear();
        AllDayCheckBox.IsChecked = false;
        NotificationMinutesTextBox.Text = "10";

        var start = DateTime.Now.AddHours(1);
        start = new DateTime(start.Year, start.Month, start.Day, start.Hour, 0, 0);
        var end = start.AddHours(1);
        StartDatePicker.SelectedDate = start.Date;
        EndDatePicker.SelectedDate = end.Date;
        StartTimeTextBox.Text = start.ToString("HH:mm", CultureInfo.InvariantCulture);
        EndTimeTextBox.Text = end.ToString("HH:mm", CultureInfo.InvariantCulture);
        UpdateTimeVisibility();
    }

    private void AllDayCheckBox_Changed(object sender, RoutedEventArgs e) =>
        UpdateTimeVisibility();

    private void UpdateTimeVisibility()
    {
        var visibility = AllDayCheckBox.IsChecked == true
            ? Visibility.Collapsed
            : Visibility.Visible;
        StartTimeLabel.Visibility = visibility;
        StartTimeTextBox.Visibility = visibility;
        EndTimeLabel.Visibility = visibility;
        EndTimeTextBox.Visibility = visibility;
    }

    private IReadOnlyList<string> GetSelectedCalendarIds() =>
        Calendars.Where(x => x.IsSelected).Select(x => x.Id).ToList();

    private async Task RunUiActionAsync(Func<Task> action)
    {
        try
        {
            IsEnabled = false;
            await action();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or ArgumentException)
        {
            ShowError(ex.Message);
        }
        finally
        {
            IsEnabled = true;
        }
    }

    private static bool TryReadTime(string value, out TimeSpan time) =>
        TimeSpan.TryParseExact(value.Trim(), TimeFormats, CultureInfo.InvariantCulture, out time)
        && time >= TimeSpan.Zero
        && time < TimeSpan.FromDays(1);

    private static DateTimeOffset ToLocalDateTimeOffset(DateTime localDateTime)
    {
        var unspecified = DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);
        return new DateTimeOffset(unspecified, TimeZoneInfo.Local.GetUtcOffset(unspecified));
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ShowError(string message) =>
        MessageBox.Show(message, "NeverForget", MessageBoxButton.OK, MessageBoxImage.Warning);

    private sealed record EventEditorInput(
        string CalendarId,
        string Title,
        string? Description,
        string? Location,
        DateTimeOffset StartsAt,
        DateTimeOffset EndsAt,
        bool IsAllDay,
        int NotificationMinutesBefore);
}
