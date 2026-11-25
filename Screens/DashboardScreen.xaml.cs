using System;
using System.Windows;
using System.Windows.Controls;

namespace QuickPos.Screens;

public partial class DashboardScreen : UserControl
{
    public event EventHandler<string>? ScreenRequested;

    public DashboardScreen()
    {
        InitializeComponent();
    }

    private void ToggleRecentlyViewed_Click(object sender, RoutedEventArgs e)
    {
        if (ContentRecentlyViewed.Visibility == Visibility.Visible)
        {
            ContentRecentlyViewed.Visibility = Visibility.Collapsed;
            ArrowRecentlyViewed.Text = "▼";
            RowRecentlyViewed.Height = GridLength.Auto;
        }
        else
        {
            ContentRecentlyViewed.Visibility = Visibility.Visible;
            ArrowRecentlyViewed.Text = "▲";
            RowRecentlyViewed.Height = new GridLength(1, GridUnitType.Star);
        }
    }

    private void ToggleFavourite_Click(object sender, RoutedEventArgs e)
    {
        if (ContentFavourite.Visibility == Visibility.Visible)
        {
            ContentFavourite.Visibility = Visibility.Collapsed;
            ArrowFavourite.Text = "▼";
        }
        else
        {
            ContentFavourite.Visibility = Visibility.Visible;
            ArrowFavourite.Text = "▲";
        }
    }

    private void SalesBill_Click(object sender, RoutedEventArgs e) =>
        ScreenRequested?.Invoke(this, "SalesBill");

    private void Items_Click(object sender, RoutedEventArgs e) =>
        ScreenRequested?.Invoke(this, "Products");

    private void Inventory_Click(object sender, RoutedEventArgs e) =>
        ScreenRequested?.Invoke(this, "Inventory");

    private void Settings_Click(object sender, RoutedEventArgs e) =>
        ScreenRequested?.Invoke(this, "Settings");

    private void Customers_Click(object sender, RoutedEventArgs e) =>
        ScreenRequested?.Invoke(this, "Customers");

    private void MenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            MessageBox.Show($"Opening: {btn.Content}", "QuickPOS", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
