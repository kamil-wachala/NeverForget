using System.Windows;
using NeverForget.Contracts;

namespace NeverForget.Client;

public partial class ReminderPopup : Window
{
    public bool WasAcknowledged { get; private set; }

    public ReminderPopup(ReminderDto reminder)
    {
        InitializeComponent();
        TitleTextBlock.Text = reminder.Title;
        MessageTextBlock.Text = reminder.Message;
        ScheduledAtTextBlock.Text = $"Scheduled for: {reminder.ScheduledAt.LocalDateTime:f}";
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        WasAcknowledged = true;
        DialogResult = true;
    }
}
