using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using NeverForget.Scheduling;

namespace NeverForget.Client;

public partial class CronEditor : UserControl
{
    private static readonly string[] TimeFormats = ["h\\:mm", "hh\\:mm"];
    private bool _isUpdating;

    public CronEditor()
    {
        InitializeComponent();

        TimeZoneComboBox.ItemsSource = TimeZoneInfo.GetSystemTimeZones()
            .Select(x => new TimeZoneOption(CronSchedule.GetPortableTimeZoneId(x), x.DisplayName))
            .DistinctBy(x => x.Id)
            .OrderBy(x => x.DisplayName)
            .ToList();

        ScheduleTypeComboBox.SelectedIndex = 2;
        SelectTimeZone(CronSchedule.GetPortableTimeZoneId(TimeZoneInfo.Local));
        Loaded += (_, _) => UpdateEditor();
    }

    public void Reset()
    {
        _isUpdating = true;
        ScheduleTypeComboBox.SelectedIndex = 2;
        DailyTimeTextBox.Text = DateTime.Now.AddMinutes(5).ToString("HH:mm", CultureInfo.InvariantCulture);
        SelectTimeZone(CronSchedule.GetPortableTimeZoneId(TimeZoneInfo.Local));
        _isUpdating = false;
        UpdateEditor();
    }

    public void SetSchedule(string cronExpression, string timeZoneId)
    {
        _isUpdating = true;
        CustomCronTextBox.Text = cronExpression;
        SelectTimeZone(timeZoneId);

        var everyMinutes = Regex.Match(cronExpression, @"^\*/(\d+) \* \* \* \*$");
        var hourly = Regex.Match(cronExpression, @"^(\d+) \* \* \* \*$");
        var daily = Regex.Match(cronExpression, @"^(\d+) (\d+) \* \* \*$");
        var weekly = Regex.Match(cronExpression, @"^(\d+) (\d+) \* \* ([0-6](?:,[0-6])*)$");
        var monthly = Regex.Match(cronExpression, @"^(\d+) (\d+) (\d+) \* \*$");

        if (everyMinutes.Success)
        {
            ScheduleTypeComboBox.SelectedIndex = 0;
            IntervalMinutesTextBox.Text = everyMinutes.Groups[1].Value;
        }
        else if (hourly.Success)
        {
            ScheduleTypeComboBox.SelectedIndex = 1;
            HourlyMinuteTextBox.Text = hourly.Groups[1].Value;
        }
        else if (daily.Success)
        {
            ScheduleTypeComboBox.SelectedIndex = 2;
            DailyTimeTextBox.Text = FormatTime(daily.Groups[2].Value, daily.Groups[1].Value);
        }
        else if (weekly.Success)
        {
            ScheduleTypeComboBox.SelectedIndex = 3;
            WeeklyTimeTextBox.Text = FormatTime(weekly.Groups[2].Value, weekly.Groups[1].Value);
            SetWeekDays(weekly.Groups[3].Value.Split(','));
        }
        else if (monthly.Success)
        {
            ScheduleTypeComboBox.SelectedIndex = 4;
            MonthlyDayTextBox.Text = monthly.Groups[3].Value;
            MonthlyTimeTextBox.Text = FormatTime(monthly.Groups[2].Value, monthly.Groups[1].Value);
        }
        else
        {
            ScheduleTypeComboBox.SelectedIndex = 5;
        }

        _isUpdating = false;
        UpdateEditor();
    }

    public bool TryGetSchedule(out string cronExpression, out string timeZoneId, out string? error)
    {
        cronExpression = BuildExpression(out error);
        timeZoneId = (TimeZoneComboBox.SelectedItem as TimeZoneOption)?.Id ?? string.Empty;

        if (error is not null)
        {
            return false;
        }

        return CronSchedule.TryValidate(cronExpression, timeZoneId, out error);
    }

    private void ScheduleInput_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isUpdating && IsLoaded)
        {
            UpdateEditor();
        }
    }

    private void UpdateEditor()
    {
        if (_isUpdating)
        {
            return;
        }

        EveryMinutesPanel.Visibility = ScheduleTypeComboBox.SelectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
        HourlyPanel.Visibility = ScheduleTypeComboBox.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        DailyPanel.Visibility = ScheduleTypeComboBox.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        WeeklyPanel.Visibility = ScheduleTypeComboBox.SelectedIndex == 3 ? Visibility.Visible : Visibility.Collapsed;
        MonthlyPanel.Visibility = ScheduleTypeComboBox.SelectedIndex == 4 ? Visibility.Visible : Visibility.Collapsed;
        CustomPanel.Visibility = ScheduleTypeComboBox.SelectedIndex == 5 ? Visibility.Visible : Visibility.Collapsed;

        var expression = BuildExpression(out var inputError);
        CronExpressionTextBlock.Text = string.IsNullOrWhiteSpace(expression) ? "—" : expression;
        var timeZoneId = (TimeZoneComboBox.SelectedItem as TimeZoneOption)?.Id ?? string.Empty;

        string? scheduleError = null;
        if (inputError is not null || !CronSchedule.TryValidate(expression, timeZoneId, out scheduleError))
        {
            ValidationTextBlock.Text = inputError ?? scheduleError;
            NextRunsItemsControl.ItemsSource = Array.Empty<string>();
            return;
        }

        ValidationTextBlock.Text = string.Empty;
        var nextRuns = new List<string>();
        var cursor = DateTimeOffset.UtcNow;
        var timeZone = CronSchedule.ResolveTimeZone(timeZoneId);
        for (var i = 0; i < 5; i++)
        {
            var occurrence = CronSchedule.GetNextOccurrence(expression, timeZoneId, cursor);
            var localOccurrence = TimeZoneInfo.ConvertTime(occurrence, timeZone);
            nextRuns.Add(localOccurrence.ToString("ddd, dd MMM yyyy HH:mm zzz", CultureInfo.GetCultureInfo("en-GB")));
            cursor = occurrence;
        }

        NextRunsItemsControl.ItemsSource = nextRuns;
    }

    private string BuildExpression(out string? error)
    {
        error = null;
        switch (ScheduleTypeComboBox.SelectedIndex)
        {
            case 0:
                if (!TryReadNumber(IntervalMinutesTextBox.Text, 1, 59, "Interval", out var interval, out error))
                {
                    return string.Empty;
                }
                return $"*/{interval} * * * *";

            case 1:
                if (!TryReadNumber(HourlyMinuteTextBox.Text, 0, 59, "Minute", out var minute, out error))
                {
                    return string.Empty;
                }
                return $"{minute} * * * *";

            case 2:
                return TryReadTime(DailyTimeTextBox.Text, out var dailyTime, out error)
                    ? $"{dailyTime.Minutes} {dailyTime.Hours} * * *"
                    : string.Empty;

            case 3:
                if (!TryReadTime(WeeklyTimeTextBox.Text, out var weeklyTime, out error))
                {
                    return string.Empty;
                }
                var days = GetSelectedWeekDays();
                if (days.Count == 0)
                {
                    error = "Select at least one day of the week.";
                    return string.Empty;
                }
                return $"{weeklyTime.Minutes} {weeklyTime.Hours} * * {string.Join(',', days)}";

            case 4:
                if (!TryReadNumber(MonthlyDayTextBox.Text, 1, 31, "Day of month", out var day, out error)
                    || !TryReadTime(MonthlyTimeTextBox.Text, out var monthlyTime, out error))
                {
                    return string.Empty;
                }
                return $"{monthlyTime.Minutes} {monthlyTime.Hours} {day} * *";

            case 5:
                return CustomCronTextBox.Text.Trim();

            default:
                error = "Select a schedule type.";
                return string.Empty;
        }
    }

    private IReadOnlyList<int> GetSelectedWeekDays()
    {
        var result = new List<int>();
        if (MondayCheckBox.IsChecked == true) result.Add(1);
        if (TuesdayCheckBox.IsChecked == true) result.Add(2);
        if (WednesdayCheckBox.IsChecked == true) result.Add(3);
        if (ThursdayCheckBox.IsChecked == true) result.Add(4);
        if (FridayCheckBox.IsChecked == true) result.Add(5);
        if (SaturdayCheckBox.IsChecked == true) result.Add(6);
        if (SundayCheckBox.IsChecked == true) result.Add(0);
        return result;
    }

    private void SetWeekDays(IEnumerable<string> days)
    {
        var selected = days.ToHashSet(StringComparer.Ordinal);
        MondayCheckBox.IsChecked = selected.Contains("1");
        TuesdayCheckBox.IsChecked = selected.Contains("2");
        WednesdayCheckBox.IsChecked = selected.Contains("3");
        ThursdayCheckBox.IsChecked = selected.Contains("4");
        FridayCheckBox.IsChecked = selected.Contains("5");
        SaturdayCheckBox.IsChecked = selected.Contains("6");
        SundayCheckBox.IsChecked = selected.Contains("0");
    }

    private void SelectTimeZone(string timeZoneId)
    {
        var options = (IEnumerable<TimeZoneOption>)TimeZoneComboBox.ItemsSource;
        TimeZoneComboBox.SelectedItem = options.FirstOrDefault(x =>
            string.Equals(x.Id, timeZoneId, StringComparison.OrdinalIgnoreCase))
            ?? options.FirstOrDefault(x => x.Id == "UTC")
            ?? options.First();
    }

    private static bool TryReadNumber(
        string value,
        int minimum,
        int maximum,
        string fieldName,
        out int result,
        out string? error)
    {
        if (int.TryParse(value, out result) && result >= minimum && result <= maximum)
        {
            error = null;
            return true;
        }

        error = $"{fieldName} must be between {minimum} and {maximum}.";
        return false;
    }

    private static bool TryReadTime(string value, out TimeSpan result, out string? error)
    {
        if (TimeSpan.TryParseExact(value.Trim(), TimeFormats, CultureInfo.InvariantCulture, out result)
            && result >= TimeSpan.Zero
            && result < TimeSpan.FromDays(1))
        {
            error = null;
            return true;
        }

        error = "Enter a valid time in HH:mm format.";
        return false;
    }

    private static string FormatTime(string hour, string minute) =>
        $"{int.Parse(hour, CultureInfo.InvariantCulture):00}:{int.Parse(minute, CultureInfo.InvariantCulture):00}";

    private sealed record TimeZoneOption(string Id, string DisplayName);
}
