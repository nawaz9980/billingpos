using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QuickPos.Components;
using QuickPos.Services;

namespace QuickPos.Screens;

public class ReturnBillRow : INotifyPropertyChanged
{
    private bool _isSelected;
    private int _returnQty;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(RefundAmount));
        }
    }

    public string Code { get; set; } = string.Empty;
    public string Item { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public int SoldQty { get; set; }

    public int ReturnQty
    {
        get => _returnQty;
        set
        {
            int clamped = Math.Clamp(value, 0, SoldQty);
            if (_returnQty == clamped) return;
            _returnQty = clamped;
            OnPropertyChanged();
            OnPropertyChanged(nameof(RefundAmount));
        }
    }

    public decimal DiscPct { get; set; }
    public decimal RefundAmount => IsSelected ? Math.Round((Rate * ReturnQty) * (1 - DiscPct / 100m), 2) : 0m;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public partial class SalesReturnScreen : UserControl
{
    private const decimal GstRate = 0.08m;
    private readonly ObservableCollection<ReturnBillRow> _returnItems = [];
    private SavedBillRecord? _loadedBill;

    public event EventHandler? CloseRequested;

    public SalesReturnScreen()
    {
        InitializeComponent();

        DpDateFrom.SelectedDate = DateTime.Today.AddDays(-7);
        DpDateTo.SelectedDate = DateTime.Today;
        GridReturnItems.ItemsSource = _returnItems;

        ReturnBillsOverlay.BillSelected += OnBillSelectedForReturn;
        ReturnBillsOverlay.CloseRequested += () => TxtBillNoInput.Focus();
        ReturnedBillsOverlay.CloseRequested += () => TxtBillNoInput.Focus();
        ReturnTenderOverlay.PaymentConfirmed += OnReturnPaymentConfirmed;
        ReturnTenderOverlay.CancelRequested += () => GridReturnItems.Focus();
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        TxtBillNoInput.Focus();
        CalculateRefundTotals();
    }

    // ════════════════════ BILL SELECTION & LOADING ════════════════════

    private void TxtBillNoInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            string query = TxtBillNoInput.Text.Trim();
            if (!string.IsNullOrEmpty(query))
            {
                var exactMatch = BillHistoryStore.GetBills()
                    .FirstOrDefault(b => b.BillNo.Equals(query, StringComparison.OrdinalIgnoreCase));
                if (exactMatch != null)
                {
                    OnBillSelectedForReturn(exactMatch);
                    return;
                }
            }
            OpenBillsList_Click(sender, e);
        }
    }

    private void OpenBillsList_Click(object sender, RoutedEventArgs e)
    {
        ReturnBillsOverlay.Show(
            TxtBillNoInput.Text.Trim(),
            DpDateFrom.SelectedDate,
            DpDateTo.SelectedDate);
    }

    private void OnBillSelectedForReturn(SavedBillRecord bill)
    {
        _loadedBill = bill;
        TxtBillNoInput.Text = bill.BillNo;
        TxtLoadedBillNo.Text = $"{bill.Prefix}-{bill.BillNo}";
        TxtLoadedDate.Text = bill.Date;
        TxtLoadedCustomer.Text = bill.Customer;
        TxtLoadedOrigAmt.Text = $"Rs. {bill.Amount:F2}";

        bool autoSelect = bill.Items.Count == 1;
        _returnItems.Clear();
        foreach (var item in bill.Items)
        {
            _returnItems.Add(new ReturnBillRow
            {
                IsSelected = autoSelect,
                Code = item.Code,
                Item = item.Item,
                Rate = item.Rate,
                SoldQty = item.Qty,
                ReturnQty = item.Qty,
                DiscPct = item.DiscPct
            });
        }

        // Hide Select Bill panel once bill is loaded, and show Loaded Bill strip
        SelectBillPanel.Visibility = Visibility.Collapsed;
        LoadedBillStrip.Visibility = Visibility.Visible;

        CalculateRefundTotals();
        GridReturnItems.Focus();
    }

    // ════════════════════ MULTI-ITEM SELECTION & TOTALS ════════════════════

    private void ToggleSelectAll_Click(object sender, RoutedEventArgs e)
    {
        if (_returnItems.Count == 0) return;

        bool selectAll = _returnItems.Any(r => !r.IsSelected);
        foreach (var row in _returnItems)
        {
            row.IsSelected = selectAll;
            if (selectAll && row.ReturnQty == 0)
            {
                row.ReturnQty = row.SoldQty;
            }
        }

        CalculateRefundTotals();
    }

    private void ReturnCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox cb && cb.DataContext is ReturnBillRow row && row.IsSelected && row.ReturnQty == 0)
        {
            row.ReturnQty = row.SoldQty;
        }
        CalculateRefundTotals();
    }

    private void GridReturnItems_CellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
    {
        Dispatcher.InvokeAsync(() =>
        {
            if (e.Row.Item is ReturnBillRow row && row.ReturnQty > 0)
            {
                row.IsSelected = true;
            }
            CalculateRefundTotals();
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    private void CalculateRefundTotals()
    {
        if (TxtSelectedCount == null || TxtReturnQtyTotal == null || TxtReturnSubTot == null ||
            TxtReturnTax == null || TxtReturnRounding == null || TxtTotalRefund == null)
            return;

        var selected = _returnItems.Where(r => r.IsSelected && r.ReturnQty > 0).ToList();
        decimal subTot = selected.Sum(r => r.RefundAmount);
        decimal tax = Math.Round(subTot * GstRate, 2);
        decimal rawTotal = subTot + tax;
        decimal roundedTotal = rawTotal > 0m ? Math.Ceiling(rawTotal - 0.50m) : 0m;
        decimal rounding = roundedTotal - rawTotal;

        TxtSelectedCount.Text = selected.Count.ToString();
        TxtReturnQtyTotal.Text = selected.Sum(r => r.ReturnQty).ToString();
        TxtReturnSubTot.Text = subTot.ToString("F2");
        TxtReturnTax.Text = tax.ToString("F2");
        TxtReturnRounding.Text = rounding.ToString("+0.00;-0.00;0.00");
        TxtTotalRefund.Text = roundedTotal.ToString("F2");
    }

    // ════════════════════ SAVE RETURN (OPENS CASH & RRN TENDER OVERLAY) ════════════════════

    private void ProcessReturn_Click(object sender, RoutedEventArgs e)
    {
        if (_loadedBill == null)
        {
            MessageBox.Show("Please select a bill to return first.", "Sales Return", MessageBoxButton.OK, MessageBoxImage.Warning);
            OpenBillsList_Click(sender, e);
            return;
        }

        var selectedItems = _returnItems.Where(r => r.IsSelected && r.ReturnQty > 0).ToList();
        if (selectedItems.Count == 0)
        {
            MessageBox.Show("Please select at least one sold item to return.", "Sales Return", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Check if any selected item has already been returned
        foreach (var sel in selectedItems)
        {
            var orig = _loadedBill.Items.FirstOrDefault(i => i.Code == sel.Code);
            int remaining = orig?.RemainingQty ?? 0;
            if (remaining <= 0)
            {
                MessageBox.Show(
                    $"'{sel.Item}' in Bill {_loadedBill.BillNo} is already returned!",
                    "Already Returned",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }
            if (sel.ReturnQty > remaining)
            {
                MessageBox.Show(
                    $"Cannot return {sel.ReturnQty} of '{sel.Item}' in Bill {_loadedBill.BillNo}.\nOnly {remaining} left to return (already returned: {orig?.ReturnedQty ?? 0}).",
                    "Already Returned",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }
        }

        decimal.TryParse(TxtTotalRefund.Text, out decimal totalRefund);
        ReturnTenderOverlay.Show($"RTN-{_loadedBill.BillNo}", totalRefund, selectedItems.Count, returnMode: true);
    }

    private void OnReturnPaymentConfirmed(PaymentResult payment)
    {
        if (_loadedBill == null) return;

        var selectedItems = _returnItems.Where(r => r.IsSelected && r.ReturnQty > 0).ToList();
        decimal totalRefund = payment.AmountDue;
        string reason = (CmbReturnReason.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Customer Return";
        // Save return without printing (No thermal print in Sales Return)
        string savedReturnNo = BillHistoryStore.AddBill(
            "RTN",
            totalRefund,
            _loadedBill.Customer,
            false,
            "RRN",
            selectedItems.Select(r => new SavedBillItem
            {
                Code = r.Code,
                Item = r.Item,
                Rate = r.Rate,
                Qty = r.ReturnQty,
                DiscPct = r.DiscPct
            }),
            payment.Rrn,
            _loadedBill.CustCode);

        // Mark returned quantities on the original bill so they cannot be returned again
        DatabaseService.MarkItemsReturned(_loadedBill.BillNo, selectedItems.Select(r => (r.Code, r.ReturnQty)));

        // Return the returned quantities back to SQLite product stock
        foreach (var item in selectedItems)
        {
            DatabaseService.RestoreStock(item.Code, item.ReturnQty);
        }

        string rrnInfo = string.IsNullOrWhiteSpace(payment.Rrn) ? "" : $"\nRRN: {payment.Rrn}";
        MessageBox.Show(
            $"Sales Return Saved!\nReturn Bill: {savedReturnNo}\nReturned Items: {selectedItems.Count}\nRefund Total: Rs. {totalRefund:F2}\nTender: {payment.TenderType}{rrnInfo}\nReason: {reason}",
            "Return Saved",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        ClearReturnState();
    }

    private void ClearReturn_Click(object sender, RoutedEventArgs e)
    {
        if (_returnItems.Count > 0)
        {
            var res = MessageBox.Show("Clear the current return screen?", "Clear Return", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res != MessageBoxResult.Yes) return;
        }
        ClearReturnState();
    }

    private void ClearReturnState()
    {
        _loadedBill = null;
        _returnItems.Clear();
        TxtBillNoInput.Clear();
        TxtCounter.Clear();
        TxtLoadedBillNo.Text = "-";
        TxtLoadedDate.Text = "-";
        TxtLoadedCustomer.Text = "-";
        TxtLoadedOrigAmt.Text = "0.00";

        SelectBillPanel.Visibility = Visibility.Visible;
        LoadedBillStrip.Visibility = Visibility.Collapsed;

        CalculateRefundTotals();
        TxtBillNoInput.Focus();
    }

    private void ViewReturnedBills_Click(object sender, RoutedEventArgs e)
    {
        ReturnedBillsOverlay.Show(returnsOnly: true);
    }

    private void CloseReturn_Click(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (ReturnTenderOverlay.Visibility == Visibility.Visible)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                ReturnTenderOverlay.Hide();
                GridReturnItems.Focus();
            }
            return;
        }

        if (ReturnedBillsOverlay.Visibility == Visibility.Visible)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                ReturnedBillsOverlay.Hide();
                TxtBillNoInput.Focus();
            }
            return;
        }

        if (ReturnBillsOverlay.Visibility == Visibility.Visible)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                ReturnBillsOverlay.Hide();
                TxtBillNoInput.Focus();
            }
            return;
        }

        if (e.Key == Key.F5)
        {
            e.Handled = true;
            OpenBillsList_Click(sender, e);
        }
        else if (e.Key == Key.F6)
        {
            e.Handled = true;
            ViewReturnedBills_Click(sender, e);
        }
        else if (e.Key == Key.F2)
        {
            e.Handled = true;
            ProcessReturn_Click(sender, e);
        }
        else if (e.Key == Key.F7)
        {
            e.Handled = true;
            ClearReturn_Click(sender, e);
        }
        else if (e.Key == Key.F12)
        {
            e.Handled = true;
            CloseReturn_Click(sender, e);
        }
    }
}
