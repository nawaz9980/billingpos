using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QuickPos.Services;

namespace QuickPos.Components;

public partial class DistributorLookupOverlay : UserControl
{
    public event Action<DatabaseService.DistributorRecord>? DistributorSelected;
    public event Action? CloseRequested;

    private List<DatabaseService.DistributorRecord> _all = [];

    public DistributorLookupOverlay()
    {
        InitializeComponent();
    }

    public void Show(string initialQuery = "")
    {
        Visibility = Visibility.Visible;
        TxtSearch.Text = initialQuery;
        LoadDistributors(initialQuery);
        Dispatcher.InvokeAsync(() =>
        {
            TxtSearch.Focus();
            TxtSearch.CaretIndex = TxtSearch.Text.Length;
        }, System.Windows.Threading.DispatcherPriority.Input);
    }

    public void Hide()
    {
        Visibility = Visibility.Collapsed;
    }

    private void LoadDistributors(string query)
    {
        _all = DatabaseService.GetDistributors(query);
        GridDistributors.ItemsSource = _all;
        if (_all.Count > 0)
            GridDistributors.SelectedIndex = 0;
        UpdateRecordCount();
    }

    private void UpdateRecordCount()
    {
        int total = _all.Count;
        int current = GridDistributors.SelectedIndex >= 0 ? GridDistributors.SelectedIndex + 1 : 0;
        TxtRecordsCount.Text = $"Records : {current}/{total}";
    }

    private void SelectCurrent()
    {
        if (GridDistributors.SelectedItem is DatabaseService.DistributorRecord dist)
        {
            Hide();
            DistributorSelected?.Invoke(dist);
        }
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        LoadDistributors(TxtSearch.Text);
    }

    private void TxtSearch_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down)
        {
            if (_all.Count > 0)
            {
                int next = Math.Min(_all.Count - 1, GridDistributors.SelectedIndex + 1);
                GridDistributors.SelectedIndex = next;
                GridDistributors.ScrollIntoView(GridDistributors.SelectedItem);
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            if (_all.Count > 0)
            {
                int prev = Math.Max(0, GridDistributors.SelectedIndex - 1);
                GridDistributors.SelectedIndex = prev;
                GridDistributors.ScrollIntoView(GridDistributors.SelectedItem);
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            SelectCurrent();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Hide();
            CloseRequested?.Invoke();
            e.Handled = true;
        }
    }

    private void GridDistributors_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SelectCurrent();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Hide();
            CloseRequested?.Invoke();
            e.Handled = true;
        }
    }

    private void GridDistributors_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        SelectCurrent();
    }

    private void GridDistributors_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateRecordCount();
    }

    private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Hide();
            CloseRequested?.Invoke();
            e.Handled = true;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        CloseRequested?.Invoke();
    }
}
