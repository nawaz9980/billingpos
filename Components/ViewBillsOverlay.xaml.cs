using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QuickPos.Services;

namespace QuickPos.Components;

public partial class ViewBillsOverlay : UserControl
{
    private List<SavedBillRecord> _filteredBills = [];
    private DateTime? _dateFrom;
    private DateTime? _dateTo;
    private bool _returnsOnly;

    public event Action<SavedBillRecord>? BillSelected;
    public event Action? CloseRequested;

    public ViewBillsOverlay()
    {
        InitializeComponent();
        ApplyFilter(string.Empty);
    }

    public string AddBillRecord(string billNo, decimal amount, string customer, bool printed, string tenderType, IEnumerable<SavedBillItem>? items = null, string rrn = "", string custCode = "")
    {
        return BillHistoryStore.AddBill(billNo, amount, customer, printed, tenderType, items ?? [], rrn, custCode);
    }

    public void Show(string initialQuery = "", DateTime? dateFrom = null, DateTime? dateTo = null, bool returnsOnly = false)
    {
        _dateFrom = dateFrom?.Date;
        _dateTo = dateTo?.Date;
        _returnsOnly = returnsOnly;

        if (TxtHeaderTitle != null)
        {
            TxtHeaderTitle.Text = returnsOnly ? "Last 50 Return Bill(s) - View" : "Last 50 bill(s) - View";
        }

        Visibility = Visibility.Visible;
        TxtSearch.Text = initialQuery;
        ApplyFilter(initialQuery);
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
        var all = BillHistoryStore.GetBills();

        _filteredBills = all.Where(b =>
        {
            if (_returnsOnly && !b.IsReturn) return false;
            if (!_returnsOnly && BillSelected != null && b.IsReturn) return false;

            if (_dateFrom.HasValue && b.BillDate < _dateFrom.Value) return false;
            if (_dateTo.HasValue && b.BillDate > _dateTo.Value) return false;

            if (string.IsNullOrEmpty(query)) return true;

            return b.BillNo.ToLowerInvariant().Contains(query) ||
                   b.Customer.ToLowerInvariant().Contains(query) ||
                   b.CustCode.ToLowerInvariant().Contains(query) ||
                   b.Prefix.ToLowerInvariant().Contains(query);
        }).ToList();

        GridBills.ItemsSource = _filteredBills;
        if (_filteredBills.Count > 0)
        {
            GridBills.SelectedIndex = 0;
            GridBills.ScrollIntoView(_filteredBills[0]);
        }
        UpdateRecordsCount();
    }

    private void UpdateRecordsCount()
    {
        int sel = GridBills.SelectedIndex >= 0 ? GridBills.SelectedIndex + 1 : (_filteredBills.Count > 0 ? 1 : 0);
        TxtRecordsCount.Text = $"Records : {sel}/{_filteredBills.Count}";
    }

    private void ConfirmOrInspectSelected()
    {
        if (GridBills.SelectedItem is not SavedBillRecord bill) return;

        if (BillSelected != null)
        {
            Hide();
            BillSelected.Invoke(bill);
        }
        else
        {
            MessageBox.Show(
                $"Bill No: {bill.Prefix}-{bill.BillNo}\nDate: {bill.Date}\nCustomer: {bill.Customer} (Code: {bill.CustCode})\nAmount: Rs. {bill.Amount:F2}\nUser: {bill.User}  |  Printed: {bill.Print}",
                "Bill Details",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter(TxtSearch.Text);
    }

    private void TxtSearch_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down && GridBills.SelectedIndex < _filteredBills.Count - 1)
        {
            e.Handled = true;
            GridBills.SelectedIndex++;
            GridBills.ScrollIntoView(GridBills.SelectedItem);
        }
        else if (e.Key == Key.Up && GridBills.SelectedIndex > 0)
        {
            e.Handled = true;
            GridBills.SelectedIndex--;
            GridBills.ScrollIntoView(GridBills.SelectedItem);
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            ConfirmOrInspectSelected();
        }
    }

    private void GridBills_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateRecordsCount();
    }

    private void GridBills_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            ConfirmOrInspectSelected();
        }
    }

    private void GridBills_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ConfirmOrInspectSelected();
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
