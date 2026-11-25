using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QuickPos.Services;

namespace QuickPos.Components;

public class CustomerRecord
{
    public int Sno { get; set; }
    public string CustCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
}

public partial class CustomerLookupOverlay : UserControl
{
    private List<CustomerRecord> _allCustomers = [];
    private List<CustomerRecord> _filteredCustomers = [];
    private string _lastSortProp = "Name";
    private ListSortDirection _lastSortDir = ListSortDirection.Ascending;

    public event Action<CustomerRecord>? CustomerSelected;
    public event Action? CloseRequested;

    public CustomerLookupOverlay()
    {
        InitializeComponent();
    }

    public void Show(string initialSearch = "")
    {
        // Load all previously saved customers sorted by Name ASC
        _allCustomers = DatabaseService.GetCustomers();
        Visibility = Visibility.Visible;
        TxtSearch.Text = initialSearch;
        ApplyFilter(initialSearch);
        TxtSearch.Focus();
        TxtSearch.SelectAll();
    }

    public void Hide()
    {
        Visibility = Visibility.Collapsed;
    }

    private void ApplyFilter(string query)
    {
        query = query.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(query))
        {
            _filteredCustomers = [.. _allCustomers];
        }
        else
        {
            _filteredCustomers = _allCustomers.Where(c =>
                c.Name.ToLowerInvariant().Contains(query) ||
                c.Phone.ToLowerInvariant().Contains(query) ||
                c.CustCode.ToLowerInvariant().Contains(query)
            ).ToList();
        }

        GridCustomers.ItemsSource = _filteredCustomers;
        if (_filteredCustomers.Count > 0)
        {
            GridCustomers.SelectedIndex = 0;
            GridCustomers.ScrollIntoView(_filteredCustomers[0]);
        }
        UpdateRecordsCount();
    }

    private void GridCustomers_Sorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;
        string prop = e.Column.SortMemberPath;

        if (_lastSortProp == prop)
        {
            _lastSortDir = _lastSortDir == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;
        }
        else
        {
            _lastSortProp = prop;
            _lastSortDir = ListSortDirection.Ascending;
        }

        IEnumerable<CustomerRecord> sorted = prop switch
        {
            "Sno" => _lastSortDir == ListSortDirection.Ascending
                ? _filteredCustomers.OrderBy(c => c.Sno)
                : _filteredCustomers.OrderByDescending(c => c.Sno),
            "CustCode" => _lastSortDir == ListSortDirection.Ascending
                ? _filteredCustomers.OrderBy(c => int.TryParse(c.CustCode, out int n) ? n : 0).ThenBy(c => c.CustCode)
                : _filteredCustomers.OrderByDescending(c => int.TryParse(c.CustCode, out int n) ? n : 0).ThenByDescending(c => c.CustCode),
            "Phone" => _lastSortDir == ListSortDirection.Ascending
                ? _filteredCustomers.OrderBy(c => c.Phone)
                : _filteredCustomers.OrderByDescending(c => c.Phone),
            _ => _lastSortDir == ListSortDirection.Ascending
                ? _filteredCustomers.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                : _filteredCustomers.OrderByDescending(c => c.Name, StringComparer.OrdinalIgnoreCase)
        };

        _filteredCustomers = sorted.ToList();
        GridCustomers.ItemsSource = _filteredCustomers;

        foreach (var col in GridCustomers.Columns)
        {
            col.SortDirection = col == e.Column ? _lastSortDir : null;
        }

        if (_filteredCustomers.Count > 0)
        {
            GridCustomers.SelectedIndex = 0;
        }
        UpdateRecordsCount();
    }

    private void UpdateRecordsCount()
    {
        int sel = GridCustomers.SelectedIndex >= 0 ? GridCustomers.SelectedIndex + 1 : (_filteredCustomers.Count > 0 ? 1 : 0);
        TxtRecordsCount.Text = $"Records : {sel}/{_filteredCustomers.Count}";
    }

    private void ConfirmSelection()
    {
        var selected = GridCustomers.SelectedItem as CustomerRecord ?? _filteredCustomers.FirstOrDefault();
        if (selected != null)
        {
            Hide();
            CustomerSelected?.Invoke(selected);
        }
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter(TxtSearch.Text);
    }

    private void TxtSearch_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down && GridCustomers.SelectedIndex < _filteredCustomers.Count - 1)
        {
            e.Handled = true;
            GridCustomers.SelectedIndex++;
            GridCustomers.ScrollIntoView(GridCustomers.SelectedItem);
        }
        else if (e.Key == Key.Up && GridCustomers.SelectedIndex > 0)
        {
            e.Handled = true;
            GridCustomers.SelectedIndex--;
            GridCustomers.ScrollIntoView(GridCustomers.SelectedItem);
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            ConfirmSelection();
        }
    }

    private void GridCustomers_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateRecordsCount();
    }

    private void GridCustomers_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            ConfirmSelection();
        }
    }

    private void GridCustomers_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ConfirmSelection();
    }

    private void SelectCustomer_Click(object sender, RoutedEventArgs e)
    {
        ConfirmSelection();
    }

    private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close_Click(sender, e);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        CloseRequested?.Invoke();
    }
}
