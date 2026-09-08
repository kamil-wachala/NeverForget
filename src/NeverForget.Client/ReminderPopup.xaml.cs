using System.Windows;
using NeverForget.Contracts;

namespace NeverForget.Client;

public partial class ReminderPopup : Window
{
    public bool WasAcknowledged { get; private set; }

    public ReminderPopup(ReminderOccurrenceDto occurrence)
    {
        InitializeComponent();
        var reminder = occurrence.Reminder;
        TitleTextBlock.Text = reminder.Title;
        MessageTextBlock.Text = reminder.Message;
        var schedule = reminder.IsRecurring
            ? $"{reminder.CronExpression} | {reminder.TimeZoneId}"
            : "One-time reminder";
        ScheduledAtTextBlock.Text =
            $"Scheduled for: {occurrence.OccursAt.LocalDateTime:f}\n{schedule}";
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        WasAcknowledged = true;
        DialogResult = true;
    }
}
