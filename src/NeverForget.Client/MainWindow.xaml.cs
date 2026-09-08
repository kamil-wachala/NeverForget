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
        FromDatePicker.SelectedDate = DateTime.Today.AddDays(-7);
        ToDatePicker.SelectedDate = DateTime.Today.AddDays(30);
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

            StatusTextBlock.Text = $"Loaded {Reminders.Count} reminders. Last updated: {DateTime.Now:T}";
        });
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadEditor(out var title, out var message, out var scheduledAt))
        {
            return;
        }

        await RunUiActionAsync(async () =>
        {
            if (_editedReminderId is Guid id)
            {
                await GetApiClient().UpdateAsync(id, new UpdateReminderRequest(title, message, scheduledAt));
                StatusTextBlock.Text = "The reminder was updated.";
            }
            else
            {
                await GetApiClient().CreateAsync(new CreateReminderRequest(title, message, scheduledAt));
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
        ScheduledDatePicker.SelectedDate = selected.ScheduledAtLocal.Date;
        ScheduledTimeTextBox.Text = selected.ScheduledAtLocal.ToString("HH:mm", CultureInfo.InvariantCulture);
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
            foreach (var reminder in dueReminders.Where(x => _displayedReminderIds.Add(x.Id)))
            {
                var popup = new ReminderPopup(reminder) { Owner = this };
                popup.ShowDialog();

                if (popup.WasAcknowledged)
                {
                    await GetApiClient().AcknowledgeAsync(reminder.Id);
                }
                else
                {
                    _displayedReminderIds.Remove(reminder.Id);
                }
            }

            if (dueReminders.Count > 0)
            {
                await RefreshRemindersAsync();
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

    private bool TryReadEditor(out string title, out string message, out DateTimeOffset scheduledAt)
    {
        title = TitleTextBox.Text.Trim();
        message = MessageTextBox.Text.Trim();
        scheduledAt = default;

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(message))
        {
            ShowError("Title and message are required.");
            return false;
        }

        if (ScheduledDatePicker.SelectedDate is not DateTime date
            || !TimeSpan.TryParseExact(ScheduledTimeTextBox.Text.Trim(), ["h\\:mm", "hh\\:mm"],
                CultureInfo.InvariantCulture, out var time))
        {
            ShowError("Select a date and enter the time in HH:mm format.");
            return false;
        }

        scheduledAt = ToLocalDateTimeOffset(date.Date.Add(time));
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
        ScheduledDatePicker.SelectedDate = DateTime.Today;
        ScheduledTimeTextBox.Text = DateTime.Now.AddMinutes(5).ToString("HH:mm", CultureInfo.InvariantCulture);
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
