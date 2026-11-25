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

public class PurchaseRow : INotifyPropertyChanged
{
    private int _qty = 1;
    private int _free;
    private decimal _purRate;
    private decimal _mrp;
    private decimal _selling;

    public int Sno { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal GstPct { get; set; }
    public bool IsTaxInclusive { get; set; }

    public int Qty
    {
        get => _qty;
        set
        {
            _qty = Math.Max(0, value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(Amount));
            OnPropertyChanged(nameof(NetAmt));
        }
    }

    public int Free
    {
        get => _free;
        set
        {
            _free = Math.Max(0, value);
            OnPropertyChanged();
        }
    }

    public decimal PurRate
    {
        get => _purRate;
        set
        {
            _purRate = Math.Max(0, value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(Amount));
            OnPropertyChanged(nameof(NetAmt));
        }
    }

    public string Batch { get; set; } = string.Empty;
    public string ExpDate { get; set; } = string.Empty;

    public decimal Mrp
    {
        get => _mrp;
        set { _mrp = value; OnPropertyChanged(); }
    }

    public decimal Selling
    {
        get => _selling;
        set { _selling = value; OnPropertyChanged(); }
    }

    public decimal Tstk { get; set; }

    public decimal Amount => Math.Round(Qty * PurRate, 2);

    public decimal TaxAmount => IsTaxInclusive
        ? Math.Round(Amount - (Amount / (1m + GstPct / 100m)), 2)
        : Math.Round(Amount * (GstPct / 100m), 2);

    public decimal NetAmt => IsTaxInclusive
        ? Amount
        : Math.Round(Amount + TaxAmount, 2);

    public void RefreshTotals()
    {
        OnPropertyChanged(nameof(Amount));
        OnPropertyChanged(nameof(NetAmt));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public partial class PurchaseInvoiceScreen : UserControl
{
    private readonly ObservableCollection<PurchaseRow> _items = [];
    private bool _isTaxInclusive;

    public event EventHandler? CloseRequested;

    public PurchaseInvoiceScreen()
    {
        InitializeComponent();

        DpGrnDate.SelectedDate = DateTime.Today;
        DpInvoiceDate.SelectedDate = DateTime.Today;
        GridPurchaseItems.ItemsSource = _items;

        PurchaseProductOverlay.ProductSelected += OnProductSelected;
        PurchaseProductOverlay.CloseRequested += () => GridPurchaseItems.Focus();

        PurchaseDistributorOverlay.DistributorSelected += OnDistributorSelected;
        PurchaseDistributorOverlay.CloseRequested += () => TxtDistCode.Focus();
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        TxtGrnNo.Text = DatabaseService.GetNextPurchaseGrnNo().ToString();
        CalculateTotals();
        TxtDistName.Focus();
    }

    private static decimal ParseGstPct(string? gstText)
    {
        if (string.IsNullOrWhiteSpace(gstText)) return 0m;
        string cleaned = new string(gstText.Where(c => char.IsDigit(c) || c == '.').ToArray());
        return decimal.TryParse(cleaned, out decimal pct) ? pct : 0m;
    }

    private void TxtDistCode_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            PurchaseDistributorOverlay.Show(TxtDistCode.Text.Trim());
        }
    }

    private void OnDistributorSelected(DatabaseService.DistributorRecord dist)
    {
        TxtDistCode.Text = dist.Code;
        TxtDistName.Text = dist.Name;
        TxtDistAddress.Text = dist.Address;
        TxtDistPhone.Text = dist.Phone;
        TxtDistType.Text = dist.DistType;
        TxtCrDays.Text = dist.CreditDays.ToString();
        TxtInvoiceNo.Focus();
    }

    private void AddItem_Click(object sender, RoutedEventArgs e)
    {
        PurchaseProductOverlay.Show();
    }

    private void OnProductSelected(ProductItem product)
    {
        decimal gstPct = ParseGstPct(product.Gst);
        _items.Add(new PurchaseRow
        {
            Sno = _items.Count + 1,
            Code = product.Code,
            Name = product.Name,
            Qty = 1,
            Free = 0,
            PurRate = product.Rate,
            Mrp = product.Rate,
            Selling = product.Rate,
            Tstk = product.Stock,
            GstPct = gstPct,
            IsTaxInclusive = _isTaxInclusive
        });

        CalculateTotals();
    }

    private void GridPurchaseItems_CellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
    {
        Dispatcher.InvokeAsync(CalculateTotals, System.Windows.Threading.DispatcherPriority.Background);
    }

    private void HeaderDiscount_TextChanged(object sender, TextChangedEventArgs e)
    {
        CalculateTotals();
    }

    private void ToggleTaxInclusive_Click(object sender, RoutedEventArgs e)
    {
        _isTaxInclusive = !_isTaxInclusive;
        TxtTaxModeBadge.Text = _isTaxInclusive ? "Tax Inclusive" : "Tax Exclusive";
        TxtTaxBtnLabel.Text = _isTaxInclusive ? "TaxExclusive" : "TaxInclusive";

        foreach (var item in _items)
        {
            item.IsTaxInclusive = _isTaxInclusive;
            item.RefreshTotals();
        }
        CalculateTotals();
    }

    private void CalculateTotals()
    {
        if (TxtGrossAmount == null || TxtGstTotal == null || TxtFinalTotal == null) return;

        decimal gross = _items.Sum(r => r.Amount);
        decimal gst = _items.Sum(r => r.TaxAmount);

        decimal.TryParse(TxtRateDeductions?.Text, out decimal rateDed);
        decimal.TryParse(TxtSchemeDiscount?.Text, out decimal schemeDisc);
        decimal.TryParse(TxtCashDiscount?.Text, out decimal cashDisc);
        decimal.TryParse(TxtFreight?.Text, out decimal freight);

        decimal rawNet = _isTaxInclusive
            ? gross - rateDed - schemeDisc - cashDisc + freight
            : gross - rateDed - schemeDisc - cashDisc + gst + freight;

        if (rawNet < 0) rawNet = 0;

        decimal roundedTotal = rawNet > 0m ? Math.Ceiling(rawNet - 0.50m) : 0m;
        decimal roundedOff = roundedTotal - rawNet;

        TxtGrossAmount.Text = gross.ToString("F2");
        TxtGstTotal.Text = gst.ToString("F2");
        TxtRoundedOff.Text = roundedOff.ToString("+0.00;-0.00;0.00");
        TxtGrnAmount.Text = roundedTotal.ToString("F2");
        TxtFinalTotal.Text = roundedTotal.ToString("F2");
        TxtOutAmt.Text = roundedTotal.ToString("F2");
        TxtSummaryGoodsTax.Text = gst.ToString("F2");

        int totQty = _items.Sum(r => r.Qty);
        int totFq = _items.Sum(r => r.Free);
        TxtTotalQty.Text = totQty.ToString();
        TxtTotalFq.Text = totFq.ToString();
        TxtTotalLcost.Text = roundedTotal.ToString("F2");

        var distinctGst = _items.Select(r => r.GstPct).Distinct().ToList();
        TxtSummaryTaxPct.Text = distinctGst.Count == 1 ? $"{distinctGst[0]:0.##}%" : "Mixed";
    }

    private void SavePurchase_Click(object sender, RoutedEventArgs e)
    {
        if (_items.Count == 0)
        {
            MessageBox.Show("Please add at least one item before saving Purchase Invoice.", "Purchase Invoice", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string grnNo = TxtGrnNo.Text.Trim();
        string invoiceNo = TxtInvoiceNo.Text.Trim();
        string distName = string.IsNullOrWhiteSpace(TxtDistName.Text) ? "Walk-in Supplier" : TxtDistName.Text.Trim();
        decimal.TryParse(TxtFinalTotal.Text, out decimal netAmt);

        DatabaseService.SavePurchaseInvoice(
            grnNo,
            DpGrnDate.SelectedDate?.ToString("dd/MM/yyyy") ?? DateTime.Today.ToString("dd/MM/yyyy"),
            invoiceNo,
            distName,
            netAmt,
            TxtNarration.Text.Trim(),
            _items.Select(i => (i.Code, i.Name, i.Qty, i.Free, i.PurRate, i.NetAmt)));

        MessageBox.Show(
            $"Purchase Invoice (GRN #{grnNo}) Saved!\nDistributor: {distName}\nTotal Amount: Rs. {netAmt:F2}\nStock has been updated in SQLite.",
            "Purchase Saved",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        ResetForm();
    }

    private void ViewPurchases_Click(object sender, RoutedEventArgs e)
    {
        var list = DatabaseService.GetRecentPurchases(20);
        if (list.Count == 0)
        {
            MessageBox.Show("No Purchase Invoices saved yet.", "Purchase History", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string summary = string.Join("\n", list.Select(p => $"GRN #{p.GrnNo} | {p.Date} | {p.Distributor} | Rs. {p.Amount:F2}"));
        MessageBox.Show(summary, "Recent Purchase Invoices", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void NewPurchase_Click(object sender, RoutedEventArgs e)
    {
        if (_items.Count > 0)
        {
            var res = MessageBox.Show("Clear current Purchase Invoice?", "New / Cancel", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res != MessageBoxResult.Yes) return;
        }
        ResetForm();
    }

    private void ResetForm()
    {
        _items.Clear();
        TxtGrnNo.Text = DatabaseService.GetNextPurchaseGrnNo().ToString();
        TxtDistCode.Clear();
        TxtParcelNo.Clear();
        TxtDistName.Clear();
        TxtDistAddress.Clear();
        TxtDistPhone.Clear();
        TxtDistType.Clear();
        TxtInvoiceNo.Clear();
        TxtInvoiceAmt.Text = "0.00";
        TxtRateDeductions.Text = "0.00";
        TxtSchemeDiscount.Text = "0.00";
        TxtCashDiscount.Text = "0.00";
        TxtFreight.Text = "0.00";
        TxtNarration.Clear();
        CalculateTotals();
        TxtDistName.Focus();
    }

    private void ClosePurchase_Click(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (PurchaseDistributorOverlay.Visibility == Visibility.Visible)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                PurchaseDistributorOverlay.Hide();
                TxtDistCode.Focus();
            }
            return;
        }

        if (PurchaseProductOverlay.Visibility == Visibility.Visible)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                PurchaseProductOverlay.Hide();
            }
            return;
        }

        if (e.Key == Key.F1 || e.Key == Key.F9)
        {
            e.Handled = true;
            AddItem_Click(sender, e);
        }
        else if (e.Key == Key.F3 || e.Key == Key.F10)
        {
            e.Handled = true;
            NewPurchase_Click(sender, e);
        }
        else if (e.Key == Key.F5)
        {
            e.Handled = true;
            ViewPurchases_Click(sender, e);
        }
        else if (e.Key == Key.F6)
        {
            e.Handled = true;
            SavePurchase_Click(sender, e);
        }
        else if (e.Key == Key.F7)
        {
            e.Handled = true;
            ToggleTaxInclusive_Click(sender, e);
        }
        else if (e.Key == Key.F12)
        {
            e.Handled = true;
            ClosePurchase_Click(sender, e);
        }
    }
}
