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
    private readonly ClientSettings _settings = ClientSettings.Load();
    private readonly DispatcherTimer _pollingTimer;
    private readonly HashSet<Guid> _displayedReminderIds = [];
    private ReminderApiClient? _apiClient;
    private string? _currentServerUrl;
    private Guid? _editedReminderId;
    private bool _isPolling;

    public ObservableCollection<ReminderListItem> Reminders { get; } = [];

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        ServerUrlTextBox.Text = _settings.ServerUrl;
        FromDatePicker.SelectedDate = DateTime.Today;
        ToDatePicker.SelectedDate = DateTime.Today.AddDays(14);
        ResetEditor();

        _pollingTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(_settings.PollingIntervalSeconds)
        };
        _pollingTimer.Tick += PollingTimer_Tick;

        Loaded += async (_, _) =>
        {
            _pollingTimer.Start();
            await RefreshRemindersAsync();
            await PollDueRemindersAsync();
        };
        Closed += (_, _) => _apiClient?.Dispose();
    }

    private ReminderApiClient GetApiClient()
    {
        var serverUrl = ServerUrlTextBox.Text.Trim();
        if (!serverUrl.EndsWith('/'))
        {
            serverUrl += '/';
        }

        if (_apiClient is not null && string.Equals(_currentServerUrl, serverUrl, StringComparison.OrdinalIgnoreCase))
        {
            return _apiClient;
        }

        _apiClient?.Dispose();
        _apiClient = new ReminderApiClient(serverUrl, _settings.ApiKey);
        _currentServerUrl = serverUrl;
        return _apiClient;
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await RefreshRemindersAsync();

    private async Task RefreshRemindersAsync()
    {
        if (FromDatePicker.SelectedDate is not DateTime fromDate || ToDatePicker.SelectedDate is not DateTime toDate)
        {
            ShowError("Select both a start date and an end date.");
            return;
        }

        if (fromDate.Date > toDate.Date)
        {
            ShowError("The start date cannot be later than the end date.");
            return;
        }

        await RunUiActionAsync(async () =>
        {
            var from = ToLocalDateTimeOffset(fromDate.Date);
            var to = ToLocalDateTimeOffset(toDate.Date.AddDays(1).AddTicks(-1));
            var items = await GetApiClient().GetBetweenAsync(from, to);

            Reminders.Clear();
            foreach (var reminder in items)
            {
                Reminders.Add(new ReminderListItem(reminder));
            }

            StatusTextBlock.Text = $"Loaded {Reminders.Count} occurrences. Last updated: {DateTime.Now:T}";
        });
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadEditor(
                out var title,
                out var message,
                out var isRecurring,
                out var scheduledAt,
                out var cronExpression,
                out var timeZoneId,
                out var endsAt))
        {
            return;
        }

        await RunUiActionAsync(async () =>
        {
            if (_editedReminderId is Guid id)
            {
                await GetApiClient().UpdateAsync(id, new UpdateReminderRequest(
                    title, message, isRecurring, scheduledAt, cronExpression, timeZoneId, endsAt));
                StatusTextBlock.Text = "The reminder was updated.";
            }
            else
            {
                await GetApiClient().CreateAsync(new CreateReminderRequest(
                    title, message, isRecurring, scheduledAt, cronExpression, timeZoneId, endsAt));
                StatusTextBlock.Text = "The reminder was added.";
            }

            ResetEditor();
            await RefreshRemindersAsync();
        });
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_editedReminderId is not Guid id
            || MessageBox.Show("Delete the selected reminder?", "NeverForget", MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        await RunUiActionAsync(async () =>
        {
            await GetApiClient().DeleteAsync(id);
            _displayedReminderIds.Remove(id);
            ResetEditor();
            await RefreshRemindersAsync();
        });
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        RemindersGrid.SelectedItem = null;
        ResetEditor();
    }

    private void RemindersGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RemindersGrid.SelectedItem is not ReminderListItem selected)
        {
            return;
        }

        _editedReminderId = selected.Id;
        EditorHeading.Text = "Edit reminder";
        SaveButton.Content = "Save";
        DeleteButton.Visibility = Visibility.Visible;
        TitleTextBox.Text = selected.Title;
        MessageTextBox.Text = selected.Message;
        IsRecurringCheckBox.IsChecked = selected.IsRecurring;
        if (selected.IsRecurring)
        {
            ScheduleEditor.SetSchedule(selected.CronExpression!, selected.TimeZoneId!, selected.EndsAt);
        }
        else if (selected.ScheduledAt is DateTimeOffset scheduledAt)
        {
            var local = scheduledAt.LocalDateTime;
            ScheduledDatePicker.SelectedDate = local.Date;
            ScheduledTimeTextBox.Text = local.ToString("HH:mm", CultureInfo.InvariantCulture);
        }
    }

    private async void PollingTimer_Tick(object? sender, EventArgs e) => await PollDueRemindersAsync();

    private async Task PollDueRemindersAsync()
    {
        if (_isPolling)
        {
            return;
        }

        _isPolling = true;
        try
        {
            var dueReminders = await GetApiClient().GetDueAsync();
            foreach (var occurrence in dueReminders.Where(x => _displayedReminderIds.Add(x.Reminder.Id)))
            {
                var popup = new ReminderPopup(occurrence) { Owner = this };
                popup.ShowDialog();

                if (popup.WasAcknowledged)
                {
                    await GetApiClient().AcknowledgeAsync(occurrence.Reminder.Id);
                    _displayedReminderIds.Remove(occurrence.Reminder.Id);
                }
                else
                {
                    _displayedReminderIds.Remove(occurrence.Reminder.Id);
                }
            }

            if (dueReminders.Count > 0)
            {
                await RefreshRemindersAsync();
            }
            else
            {
                StatusTextBlock.Text = $"Connected. Last checked: {DateTime.Now:T}";
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or ArgumentException)
        {
            StatusTextBlock.Text = $"Cannot connect to the server: {ex.Message}";
        }
        finally
        {
            _isPolling = false;
        }
    }

    private bool TryReadEditor(
        out string title,
        out string message,
        out bool isRecurring,
        out DateTimeOffset? scheduledAt,
        out string? cronExpression,
        out string? timeZoneId,
        out DateTimeOffset? endsAt)
    {
        title = TitleTextBox.Text.Trim();
        message = MessageTextBox.Text.Trim();
        isRecurring = IsRecurringCheckBox.IsChecked == true;
        scheduledAt = null;
        cronExpression = null;
        timeZoneId = null;
        endsAt = null;

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(message))
        {
            ShowError("Title and message are required.");
            return false;
        }

        if (!isRecurring)
        {
            if (!TryReadOneTimeSchedule(out scheduledAt, out var oneTimeError))
            {
                ShowError(oneTimeError!);
                return false;
            }

            return true;
        }

        if (!ScheduleEditor.TryGetSchedule(out var cron, out var zone, out endsAt, out var scheduleError))
        {
            ShowError(scheduleError ?? "Enter a valid schedule.");
            return false;
        }

        cronExpression = cron;
        timeZoneId = zone;

        return true;
    }

    private void ResetEditor()
    {
        _editedReminderId = null;
        EditorHeading.Text = "New reminder";
        SaveButton.Content = "Add";
        DeleteButton.Visibility = Visibility.Collapsed;
        TitleTextBox.Clear();
        MessageTextBox.Clear();
        IsRecurringCheckBox.IsChecked = false;
        var suggestedTime = DateTime.Now.AddMinutes(5);
        ScheduledDatePicker.SelectedDate = suggestedTime.Date;
        ScheduledTimeTextBox.Text = suggestedTime.ToString("HH:mm", CultureInfo.InvariantCulture);
        ScheduleEditor.Reset();
    }

    private void IsRecurringCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        var isRecurring = IsRecurringCheckBox.IsChecked == true;
        OneTimeSchedulePanel.Visibility = isRecurring ? Visibility.Collapsed : Visibility.Visible;
        ScheduleEditor.Visibility = isRecurring ? Visibility.Visible : Visibility.Collapsed;
    }

    private bool TryReadOneTimeSchedule(out DateTimeOffset? scheduledAt, out string? error)
    {
        scheduledAt = null;
        if (ScheduledDatePicker.SelectedDate is not DateTime date
            || !TimeSpan.TryParseExact(
                ScheduledTimeTextBox.Text.Trim(),
                ["h\\:mm", "hh\\:mm"],
                CultureInfo.InvariantCulture,
                out var time))
        {
            error = "Select a reminder date and enter a valid time in HH:mm format.";
            return false;
        }

        var localDateTime = DateTime.SpecifyKind(date.Date.Add(time), DateTimeKind.Unspecified);
        scheduledAt = new DateTimeOffset(localDateTime, TimeZoneInfo.Local.GetUtcOffset(localDateTime));
        if (scheduledAt <= DateTimeOffset.Now)
        {
            error = "The reminder date and time must be in the future.";
            return false;
        }

        error = null;
        return true;
    }

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

    private static DateTimeOffset ToLocalDateTimeOffset(DateTime localDateTime)
    {
        var unspecified = DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);
        return new DateTimeOffset(unspecified, TimeZoneInfo.Local.GetUtcOffset(unspecified));
    }

    private static void ShowError(string message) =>
        MessageBox.Show(message, "NeverForget", MessageBoxButton.OK, MessageBoxImage.Warning);
}
