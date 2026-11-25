using System;
using System.Windows;
using System.Windows.Controls;

namespace QuickPos.Components;

public partial class TopMenuBar : UserControl
{
    public event EventHandler<string>? MenuItemSelected;
    public event EventHandler? HomeClicked;
    public event EventHandler? HamburgerClicked;
    public event EventHandler? NotificationClicked;
    public event EventHandler? SearchClicked;
    public event EventHandler? AvatarClicked;

    public TopMenuBar()
    {
        InitializeComponent();
    }

    private void Logo_Click(object sender, RoutedEventArgs e)
    {
        HomeClicked?.Invoke(this, EventArgs.Empty);
    }

    private void MenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.ContextMenu != null)
        {
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            btn.ContextMenu.IsOpen = true;
        }
    }

    private void NavMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
        {
            string screenKey = menuItem.Tag?.ToString() ?? menuItem.Header?.ToString() ?? string.Empty;
            MenuItemSelected?.Invoke(this, screenKey);
        }
    }

    private void Hamburger_Click(object sender, RoutedEventArgs e) =>
        HamburgerClicked?.Invoke(this, EventArgs.Empty);

    private void Notification_Click(object sender, RoutedEventArgs e) =>
        NotificationClicked?.Invoke(this, EventArgs.Empty);

    private void Search_Click(object sender, RoutedEventArgs e) =>
        SearchClicked?.Invoke(this, EventArgs.Empty);

    private void Avatar_Click(object sender, RoutedEventArgs e) =>
        AvatarClicked?.Invoke(this, EventArgs.Empty);
}
