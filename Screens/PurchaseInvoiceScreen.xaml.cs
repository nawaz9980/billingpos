using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
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
    public bool IsBlank { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public string HsnCode { get; set; } = string.Empty;
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
            OnPropertyChanged(nameof(TaxAmount));
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
            OnPropertyChanged(nameof(TaxAmount));
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
        OnPropertyChanged(nameof(TaxAmount));
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

        QuickAddItemOverlay.ItemsCreated += OnQuickItemsCreated;
        QuickAddItemOverlay.CloseRequested += () => TxtScanBarcode.Focus();
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        TxtGrnNo.Text = DatabaseService.GetNextPurchaseGrnNo().ToString();
        EnsureTrailingBlankRow();
        CalculateTotals();
        TxtDistCode.Focus();
    }

    private void EnsureTrailingBlankRow()
    {
        for (int i = _items.Count - 1; i >= 0; i--)
        {
            if (_items[i].IsBlank && i != _items.Count - 1)
                _items.RemoveAt(i);
        }

        if (_items.Count == 0 || !_items[^1].IsBlank)
        {
            _items.Add(new PurchaseRow
            {
                IsBlank = true,
                Qty = 0,
                IsTaxInclusive = _isTaxInclusive
            });
        }

        for (int i = 0; i < _items.Count; i++)
            _items[i].Sno = i + 1;

        GridPurchaseItems.Items.Refresh();
    }

    private bool ValidateHeaderBeforeItems()
    {
        if (string.IsNullOrWhiteSpace(TxtInvoiceNo.Text))
        {
            MessageBox.Show("Please enter Invoice No before adding items.", "Header Required",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtInvoiceNo.Focus();
            return false;
        }

        if (DpInvoiceDate.SelectedDate == null)
        {
            MessageBox.Show("Please select Invoice Date before adding items.", "Header Required",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            DpInvoiceDate.Focus();
            return false;
        }

        if (!decimal.TryParse(TxtInvoiceAmt.Text.Trim(), out decimal invAmt) || invAmt <= 0m)
        {
            MessageBox.Show("Please enter Invoice Amount (> 0) before adding items.", "Header Required",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtInvoiceAmt.Focus();
            TxtInvoiceAmt.SelectAll();
            return false;
        }

        return true;
    }

    private void FocusBlankCodeCell()
    {
        EnsureTrailingBlankRow();
        var blankRow = _items[^1];
        Dispatcher.InvokeAsync(() =>
        {
            GridPurchaseItems.Focus();
            GridPurchaseItems.ScrollIntoView(blankRow);
            GridPurchaseItems.CurrentCell = new DataGridCellInfo(blankRow, GridPurchaseItems.Columns[1]);
            GridPurchaseItems.SelectedCells.Clear();
            GridPurchaseItems.SelectedCells.Add(GridPurchaseItems.CurrentCell);
        }, System.Windows.Threading.DispatcherPriority.Input);
    }

    private void FocusGridCell(PurchaseRow row, int columnIndex)
    {
        Dispatcher.InvokeAsync(() =>
        {
            GridPurchaseItems.Focus();
            GridPurchaseItems.ScrollIntoView(row);
            GridPurchaseItems.CurrentCell = new DataGridCellInfo(row, GridPurchaseItems.Columns[columnIndex]);
            GridPurchaseItems.SelectedCells.Clear();
            GridPurchaseItems.SelectedCells.Add(GridPurchaseItems.CurrentCell);
            GridPurchaseItems.BeginEdit();
        }, System.Windows.Threading.DispatcherPriority.Input);
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
        if (!ValidateHeaderBeforeItems()) return;
        PurchaseProductOverlay.Show();
    }

    private void QuickAddItem_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateHeaderBeforeItems()) return;
        QuickAddItemOverlay.Show(TxtScanBarcode.Text.Trim());
    }

    private void TxtScanBarcode_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        if (!ValidateHeaderBeforeItems()) return;

        string query = TxtScanBarcode.Text.Trim();
        if (string.IsNullOrEmpty(query))
        {
            PurchaseProductOverlay.Show();
            return;
        }

        var product = DatabaseService.GetProductByBarcodeOrCode(query);
        if (product != null)
        {
            var existing = _items.FirstOrDefault(r =>
                string.Equals(r.Code, product.Code, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.Qty += 1;
                CalculateTotals();
            }
            else
            {
                OnProductSelected(product);
            }

            TxtScanBarcode.Clear();
            TxtScanBarcode.Focus();
            return;
        }

        var res = MessageBox.Show(
            $"Barcode / Code '{query}' not found.\nCreate a new item with this barcode?",
            "Item Not Found",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (res == MessageBoxResult.Yes)
            QuickAddItemOverlay.Show(query);
    }

    private void OnQuickItemsCreated(System.Collections.Generic.List<QuickAddedPurchaseItem> created)
    {
        PurchaseRow? lastAdded = null;
        foreach (var item in created)
        {
            decimal gstPct = ParseGstPct(item.Product.Gst);
            var row = new PurchaseRow
            {
                IsBlank = false,
                Code = item.Product.Code,
                Barcode = item.Product.Barcode,
                Name = item.Product.Name,
                Size = item.Product.Size,
                HsnCode = item.Product.HsnCode,
                Qty = item.Qty,
                Free = 0,
                PurRate = item.PurRate,
                Mrp = item.Mrp,
                Selling = item.Selling,
                Tstk = item.Product.Stock,
                GstPct = gstPct,
                IsTaxInclusive = _isTaxInclusive
            };

            int insertIdx = _items.Count > 0 && _items[^1].IsBlank ? _items.Count - 1 : _items.Count;
            _items.Insert(insertIdx, row);
            lastAdded = row;
        }

        EnsureTrailingBlankRow();
        CalculateTotals();
        TxtScanBarcode.Clear();
        if (lastAdded != null)
            FocusGridCell(lastAdded, 6); // Move right to Qty
        else
            FocusBlankCodeCell();
    }

    private void PrintBarcodes_Click(object sender, RoutedEventArgs e)
    {
        var validRows = _items.Where(r => !r.IsBlank && !string.IsNullOrWhiteSpace(r.Code)).ToList();
        if (validRows.Count == 0)
        {
            MessageBox.Show("No items in Purchase Invoice to print barcode stickers.", "Print Barcode",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new PrintDialog();
        if (dlg.ShowDialog() != true) return;

        var doc = new FlowDocument
        {
            PagePadding = new Thickness(12),
            ColumnWidth = 220,
            FontFamily = new FontFamily("Segoe UI")
        };

        foreach (var row in validRows)
        {
            int copies = Math.Max(1, row.Qty);
            string codeToPrint = string.IsNullOrWhiteSpace(row.Barcode) ? row.Code : row.Barcode;

            for (int c = 0; c < copies; c++)
            {
                var card = new Border
                {
                    Margin = new Thickness(0, 0, 0, 10),
                    BorderBrush = Brushes.Gray,
                    BorderThickness = new Thickness(0.75),
                    Padding = new Thickness(7),
                    Background = Brushes.White
                };

                var stack = new StackPanel();
                stack.Children.Add(new TextBlock { Text = "QuickPOS", FontWeight = FontWeights.Bold, FontSize = 9, Foreground = Brushes.Black });
                stack.Children.Add(new TextBlock { Text = row.Name, FontWeight = FontWeights.Bold, FontSize = 10.5, Foreground = Brushes.Black, TextWrapping = TextWrapping.Wrap });

                string meta = string.IsNullOrWhiteSpace(row.Size) ? "" : $"Size: {row.Size}  ";
                if (!string.IsNullOrWhiteSpace(row.HsnCode)) meta += $"HSN: {row.HsnCode}";
                if (!string.IsNullOrEmpty(meta))
                    stack.Children.Add(new TextBlock { Text = meta.Trim(), FontSize = 9, Foreground = Brushes.Black });

                stack.Children.Add(new TextBlock
                {
                    Text = $"MRP: Rs.{row.Mrp:F2}   Rate: Rs.{row.Selling:F2}",
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 9.5,
                    Foreground = Brushes.Black
                });

                stack.Children.Add(BarcodeGenerator.CreateBarcodeElement(codeToPrint, height: 32, narrowWidth: 0.95));

                card.Child = stack;
                doc.Blocks.Add(new BlockUIContainer(card));
            }
        }

        dlg.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, "QuickPOS Barcode Labels");
    }

    private void OnProductSelected(ProductItem product)
    {
        decimal gstPct = ParseGstPct(product.Gst);
        var row = new PurchaseRow
        {
            IsBlank = false,
            Code = product.Code,
            Barcode = product.Barcode,
            Name = product.Name,
            Size = product.Size,
            HsnCode = product.HsnCode,
            Qty = 1,
            Free = 0,
            PurRate = product.Rate,
            Mrp = product.Rate,
            Selling = product.Rate,
            Tstk = product.Stock,
            GstPct = gstPct,
            IsTaxInclusive = _isTaxInclusive
        };

        int insertIdx = _items.Count > 0 && _items[^1].IsBlank ? _items.Count - 1 : _items.Count;
        _items.Insert(insertIdx, row);
        EnsureTrailingBlankRow();
        CalculateTotals();
        FocusGridCell(row, 6); // Move right to Qty
    }

    private void GridPurchaseItems_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        var current = GridPurchaseItems.CurrentCell;
        if (current.Item is not PurchaseRow row || current.Column == null) return;

        int colIdx = GridPurchaseItems.Columns.IndexOf(current.Column);
        e.Handled = true;

        // Commit any active edit before moving
        string? editedText = (Keyboard.FocusedElement as TextBox)?.Text;
        GridPurchaseItems.CommitEdit(DataGridEditingUnit.Cell, true);
        GridPurchaseItems.CommitEdit(DataGridEditingUnit.Row, true);
        CalculateTotals();

        // If pressing Enter on Code cell of the blank row -> lookup or open product list
        if (row.IsBlank && colIdx == 1)
        {
            if (!ValidateHeaderBeforeItems())
            {
                row.Code = string.Empty;
                return;
            }

            string query = (editedText ?? row.Code ?? string.Empty).Trim();
            row.Code = string.Empty;

            if (string.IsNullOrEmpty(query))
            {
                PurchaseProductOverlay.Show();
                return;
            }

            var product = DatabaseService.GetProductByBarcodeOrCode(query);
            if (product != null)
            {
                OnProductSelected(product);
                return;
            }

            var res = MessageBox.Show(
                $"Code / Barcode '{query}' not found.\nCreate a new item with this code?",
                "Item Not Found",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (res == MessageBoxResult.Yes)
                QuickAddItemOverlay.Show(query);
            else
                FocusBlankCodeCell();
            return;
        }

        // Editable columns to the right: Size(4), HSN(5), Qty(6), Free(7), PurRate(8), MRP(9), Selling(10)
        int[] editableCols = [4, 5, 6, 7, 8, 9, 10];
        int pos = Array.IndexOf(editableCols, colIdx);
        if (pos >= 0 && pos + 1 < editableCols.Length)
        {
            FocusGridCell(row, editableCols[pos + 1]);
        }
        else if (colIdx < 4)
        {
            FocusGridCell(row, 6); // From Code/Name jump to Qty
        }
        else
        {
            // End of row (Selling) -> move down to empty Code cell of trailing blank row
            FocusBlankCodeCell();
        }
    }

    private void GridPurchaseItems_BeginningEdit(object? sender, DataGridBeginningEditEventArgs e)
    {
        if (e.Row.Item is PurchaseRow row && row.IsBlank)
        {
            int colIdx = GridPurchaseItems.Columns.IndexOf(e.Column);
            if (colIdx != 1) // Only Code cell is editable on the blank row
                e.Cancel = true;
        }
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

        var validItems = _items.Where(r => !r.IsBlank && !string.IsNullOrWhiteSpace(r.Code)).ToList();
        decimal gross = validItems.Sum(r => r.Amount);
        decimal gst = validItems.Sum(r => r.TaxAmount);

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

        int totQty = validItems.Sum(r => r.Qty);
        int totFq = validItems.Sum(r => r.Free);
        TxtTotalQty.Text = totQty.ToString();
        TxtTotalFq.Text = totFq.ToString();
        TxtTotalLcost.Text = roundedTotal.ToString("F2");

        var distinctGst = validItems.Select(r => r.GstPct).Distinct().ToList();
        TxtSummaryTaxPct.Text = distinctGst.Count == 1 ? $"{distinctGst[0]:0.##}%" : "Mixed";
    }

    private void SavePurchase_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateHeaderBeforeItems()) return;

        var validItems = _items.Where(r => !r.IsBlank && !string.IsNullOrWhiteSpace(r.Code)).ToList();
        if (validItems.Count == 0)
        {
            MessageBox.Show("Please add at least one item before saving Purchase Invoice.", "Purchase Invoice", MessageBoxButton.OK, MessageBoxImage.Warning);
            FocusBlankCodeCell();
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
            validItems.Select(i => (i.Code, i.Name, i.Qty, i.Free, i.PurRate, i.NetAmt, i.Barcode, i.Size, i.HsnCode)));

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
        if (_items.Any(r => !r.IsBlank))
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
        EnsureTrailingBlankRow();
        CalculateTotals();
        TxtDistCode.Focus();
    }

    private void ClosePurchase_Click(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (QuickAddItemOverlay.Visibility == Visibility.Visible)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                QuickAddItemOverlay.Hide();
                TxtScanBarcode.Focus();
            }
            return;
        }

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
        else if (e.Key == Key.F2)
        {
            e.Handled = true;
            QuickAddItem_Click(sender, e);
        }
        else if (e.Key == Key.F8)
        {
            e.Handled = true;
            PrintBarcodes_Click(sender, e);
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
        else if (e.Key == Key.Enter)
        {
            if (HandleEnterNavigation())
                e.Handled = true;
        }
    }

    private bool HandleEnterNavigation()
    {
        var focused = Keyboard.FocusedElement as DependencyObject;
        if (focused == null) return false;

        Control[] order =
        [
            CmbGrnType,
            TxtParcelNo,
            CmbPurType,
            DpGrnDate,
            TxtInvoiceNo,
            DpInvoiceDate,
            TxtInvoiceAmt,
            TxtRateDeductions,
            TxtSchemeDiscount,
            TxtCashDiscount,
            TxtFreight,
            TxtCrDays,
            TxtNarration
        ];

        for (int i = 0; i < order.Length; i++)
        {
            if (IsInside(focused, order[i]))
            {
                if (ReferenceEquals(order[i], TxtInvoiceAmt))
                {
                    if (ValidateHeaderBeforeItems())
                        FocusBlankCodeCell();
                    return true;
                }

                if (i + 1 < order.Length)
                {
                    order[i + 1].Focus();
                    if (order[i + 1] is TextBox tb) tb.SelectAll();
                }
                else
                {
                    FocusBlankCodeCell();
                }
                return true;
            }
        }

        return false;
    }

    private static bool IsInside(DependencyObject element, DependencyObject target)
    {
        while (element != null)
        {
            if (ReferenceEquals(element, target)) return true;
            element = VisualTreeHelper.GetParent(element);
        }
        return false;
    }
}
