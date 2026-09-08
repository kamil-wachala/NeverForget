using System.Windows;
using NeverForget.Contracts;

namespace NeverForget.Client;

public partial class CalendarNotificationPopup : Window
{
    public CalendarNotificationPopup(CalendarEventNotificationDto notification)
    {
        InitializeComponent();
        var calendarEvent = notification.Event;
        TitleTextBlock.Text = calendarEvent.Title;
        MessageTextBlock.Text = string.Join(
            Environment.NewLine + Environment.NewLine,
            new[] { calendarEvent.Description, calendarEvent.Location }
                .Where(x => !string.IsNullOrWhiteSpace(x)));

        var eventTime = calendarEvent.IsAllDay
            ? $"All day on {calendarEvent.StartsAt.LocalDateTime:D}"
            : $"{calendarEvent.StartsAt.LocalDateTime:f} - {calendarEvent.EndsAt.LocalDateTime:g}";
        ScheduledAtTextBlock.Text =
            $"{eventTime}\n{calendarEvent.CalendarName}\nNotification: {calendarEvent.NotificationMinutesBefore} minutes before";
    }

    private void OkButton_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
