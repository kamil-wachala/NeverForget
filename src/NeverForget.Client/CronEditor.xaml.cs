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
        MonthlyModeComboBox.SelectedIndex = 0;
        EndDatePicker.SelectedDate = DateTime.Today.AddYears(5);
        SelectTimeZone(CronSchedule.GetPortableTimeZoneId(TimeZoneInfo.Local));
        Loaded += (_, _) => UpdateEditor();
    }

    public void Reset()
    {
        _isUpdating = true;
        ScheduleTypeComboBox.SelectedIndex = 2;
        DailyTimeTextBox.Text = DateTime.Now.AddMinutes(5).ToString("HH:mm", CultureInfo.InvariantCulture);
        MonthlyModeComboBox.SelectedIndex = 0;
        ForeverCheckBox.IsChecked = true;
        EndDatePicker.SelectedDate = DateTime.Today.AddYears(5);
        SelectTimeZone(CronSchedule.GetPortableTimeZoneId(TimeZoneInfo.Local));
        _isUpdating = false;
        UpdateEditor();
    }

    public void SetSchedule(string cronExpression, string timeZoneId, DateTimeOffset? endsAt)
    {
        _isUpdating = true;
        CustomCronTextBox.Text = cronExpression;
        SelectTimeZone(timeZoneId);

        var everyMinutes = Regex.Match(cronExpression, @"^\*/(\d+) \* \* \* \*$");
        var hourly = Regex.Match(cronExpression, @"^(\d+) \* \* \* \*$");
        var daily = Regex.Match(cronExpression, @"^(\d+) (\d+) \* \* \*$");
        var weekly = Regex.Match(cronExpression, @"^(\d+) (\d+) \* \* ([0-6](?:,[0-6])*)$");
        var monthly = Regex.Match(cronExpression, @"^(\d+) (\d+) (\d+|L|LW) \* \*$");

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
            MonthlyModeComboBox.SelectedIndex = monthly.Groups[3].Value switch
            {
                "L" => 1,
                "LW" => 2,
                _ => 0
            };
            if (MonthlyModeComboBox.SelectedIndex == 0)
            {
                MonthlyDayTextBox.Text = monthly.Groups[3].Value;
            }
            MonthlyTimeTextBox.Text = FormatTime(monthly.Groups[2].Value, monthly.Groups[1].Value);
        }
        else
        {
            ScheduleTypeComboBox.SelectedIndex = 5;
        }

        ForeverCheckBox.IsChecked = endsAt is null;
        EndDatePicker.SelectedDate = endsAt is null
            ? DateTime.Today.AddYears(5)
            : TimeZoneInfo.ConvertTime(endsAt.Value, CronSchedule.ResolveTimeZone(timeZoneId)).Date;

        _isUpdating = false;
        UpdateEditor();
    }

    public bool TryGetSchedule(
        out string cronExpression,
        out string timeZoneId,
        out DateTimeOffset? endsAt,
        out string? error)
    {
        cronExpression = BuildExpression(out error);
        timeZoneId = (TimeZoneComboBox.SelectedItem as TimeZoneOption)?.Id ?? string.Empty;
        endsAt = null;

        if (error is not null)
        {
            return false;
        }

        if (!CronSchedule.TryValidate(cronExpression, timeZoneId, out error))
        {
            return false;
        }

        if (!TryGetEndDate(timeZoneId, out endsAt, out error))
        {
            return false;
        }

        if (endsAt is not null
            && CronSchedule.GetNextOccurrence(cronExpression, timeZoneId, DateTimeOffset.UtcNow) > endsAt)
        {
            error = "The schedule has no occurrence on or before its end date.";
            return false;
        }

        return true;
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
        MonthlyDayPanel.Visibility = MonthlyModeComboBox.SelectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
        EndDatePanel.Visibility = ForeverCheckBox.IsChecked == true ? Visibility.Collapsed : Visibility.Visible;

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

        if (!TryGetEndDate(timeZoneId, out var endsAt, out scheduleError))
        {
            ValidationTextBlock.Text = scheduleError;
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
            if (endsAt is not null && occurrence > endsAt)
            {
                break;
            }
            var localOccurrence = TimeZoneInfo.ConvertTime(occurrence, timeZone);
            nextRuns.Add(localOccurrence.ToString("ddd, dd MMM yyyy HH:mm zzz", CultureInfo.GetCultureInfo("en-GB")));
            cursor = occurrence;
        }

        if (nextRuns.Count == 0 && endsAt is not null)
        {
            ValidationTextBlock.Text = "The schedule has no occurrence on or before its end date.";
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
                if (!TryReadTime(MonthlyTimeTextBox.Text, out var monthlyTime, out error))
                {
                    return string.Empty;
                }

                if (MonthlyModeComboBox.SelectedIndex == 1)
                {
                    return $"{monthlyTime.Minutes} {monthlyTime.Hours} L * *";
                }

                if (MonthlyModeComboBox.SelectedIndex == 2)
                {
                    return $"{monthlyTime.Minutes} {monthlyTime.Hours} LW * *";
                }

                if (!TryReadNumber(MonthlyDayTextBox.Text, 1, 31, "Day of month", out var day, out error))
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

    private bool TryGetEndDate(string timeZoneId, out DateTimeOffset? endsAt, out string? error)
    {
        endsAt = null;
        error = null;
        if (ForeverCheckBox.IsChecked == true)
        {
            return true;
        }

        if (EndDatePicker.SelectedDate is not DateTime endDate)
        {
            error = "Select the last date for this recurring reminder.";
            return false;
        }

        var endOfDay = DateTime.SpecifyKind(endDate.Date.AddDays(1).AddTicks(-1), DateTimeKind.Unspecified);
        var timeZone = CronSchedule.ResolveTimeZone(timeZoneId);
        endsAt = new DateTimeOffset(endOfDay, timeZone.GetUtcOffset(endOfDay));
        if (endsAt <= DateTimeOffset.UtcNow)
        {
            error = "The recurrence end date must be in the future.";
            return false;
        }

        return true;
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
