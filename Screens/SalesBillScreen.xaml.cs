using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QuickPos.Components;
using QuickPos.Services;

namespace QuickPos.Screens;

public class BillRow
{
    public string Code { get; set; } = string.Empty;
    public string Item { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public int Qty { get; set; } = 1;
    public decimal DiscPct { get; set; }
    public decimal GstPct { get; set; } = 8m;
    public decimal AvailableStock { get; set; } = decimal.MaxValue;
    public decimal Amount => Math.Round((Rate * Qty) * (1 - DiscPct / 100m), 2);
    public string SalesMan { get; set; } = "Nawaz";
}

public class BillSession
{
    public int Id { get; set; } = 1;
    public string BillNo => Id.ToString();
    public ObservableCollection<BillRow> Items { get; set; } = [];
    public string TenderedText { get; set; } = "0.00";
    public string CustomerPhone { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public bool IsHold { get; set; }
}

public partial class SalesBillScreen : UserControl
{
    private readonly List<BillSession> _openBills = [];
    private BillSession _activeBill = null!;
    private const decimal GstRate = 0.08m;

    public event EventHandler? CloseRequested;
    public event EventHandler? ReturnRequested;

    public int OpenBillsCount => _openBills.Count;
    public int ActiveBillIndex => _openBills.IndexOf(_activeBill);

    public void ActivateBillByIndex(int index)
    {
        if (index >= 0 && index < _openBills.Count)
        {
            SwitchToBill(_openBills[index]);
        }
    }

    private bool _isInitialized = false;

    public SalesBillScreen()
    {
        InitializeComponent();
        TxtBillDate.Text = DateTime.Now.ToString("dd/MM/yyyy");

        // Start with initial Bill #1
        var firstBill = new BillSession { Id = 1 };
        _openBills.Add(firstBill);
        _activeBill = firstBill;
        GridBillItems.ItemsSource = _activeBill.Items;

        // Wire Product Lookup Overlay
        ProductOverlay.ProductSelected += OnProductSelectedFromOverlay;
        ProductOverlay.CloseRequested += () => TxtBarcodeEntry.Focus();

        // Wire Payment Tender Overlay
        TenderOverlay.PaymentConfirmed += OnPaymentConfirmed;
        TenderOverlay.CancelRequested += () => TxtBarcodeEntry.Focus();

        // Wire View Bills Overlay
        BillsOverlay.CloseRequested += () => TxtBarcodeEntry.Focus();

        // Wire Customer Lookup Overlay
        CustomerOverlay.CustomerSelected += OnCustomerSelectedFromOverlay;
        CustomerOverlay.CloseRequested += () => TxtCustomerPhone.Focus();
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized)
        {
            _isInitialized = true;
            DatabaseService.EnsureInitialized();
            _activeBill.Id = DatabaseService.GetNextSalesBillNumber();
            if (TxtBillNo != null) TxtBillNo.Text = _activeBill.BillNo;
        }
        UpdateHeldIndicator();
        CalculateTotals();

        if (string.IsNullOrWhiteSpace(TxtCustomerPhone.Text))
            TxtCustomerPhone.Focus();
        else
            TxtBarcodeEntry.Focus();
    }

    // ════════════════════ HOLD BILL & MULTI-BILL LOGIC ════════════════════

    private void HoldBill_Click(object sender, RoutedEventArgs e)
    {
        // Rule 1: If current bill has no items, cannot hold
        if (_activeBill.Items.Count == 0)
        {
            MessageBox.Show(
                "Cannot hold an empty bill!\nPlease add at least one item before holding.",
                "Hold Bill - Empty",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            TxtBarcodeEntry.Focus();
            return;
        }

        // Rule 2: Only ONE empty sales bill is possible at any time!
        // If an empty bill already exists among open bills, switch to it instead of creating another.
        var existingEmptyBill = _openBills.FirstOrDefault(b => b != _activeBill && b.Items.Count == 0);
        if (existingEmptyBill != null)
        {
            _activeBill.IsHold = true;
            SwitchToBill(existingEmptyBill);

            MessageBox.Show(
                $"Current bill is placed on HOLD.\nSwitched to existing empty sales bill {existingEmptyBill.BillNo}.\n(Only 1 empty bill can exist at a time).",
                "Switched to Existing Empty Bill",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        // Mark current active bill as Held
        _activeBill.IsHold = true;

        // Create new empty bill session
        int nextId = _openBills.Max(b => b.Id) + 1;
        var newBill = new BillSession { Id = nextId, IsHold = false };
        _openBills.Add(newBill);

        // Switch to the fresh empty bill screen
        SwitchToBill(newBill);

        MessageBox.Show(
            $"Current bill is now on HOLD.\nFresh empty sales bill {_activeBill.BillNo} is open.\nPress the [Tab] key anytime to switch between bills.",
            "Bill Placed on Hold",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void SwitchToBill(BillSession target)
    {
        if (target == null || target == _activeBill) return;

        // Preserve current session's tendered & customer info
        if (TxtTendered != null) _activeBill.TenderedText = TxtTendered.Text;
        if (TxtCustomerPhone != null) _activeBill.CustomerPhone = TxtCustomerPhone.Text.Trim();
        if (TxtCustomerName != null) _activeBill.CustomerName = TxtCustomerName.Text.Trim();

        _activeBill = target;
        GridBillItems.ItemsSource = _activeBill.Items;

        if (TxtBillNo != null) TxtBillNo.Text = _activeBill.BillNo;
        if (TxtTendered != null) TxtTendered.Text = _activeBill.TenderedText;
        if (TxtCustomerPhone != null) TxtCustomerPhone.Text = _activeBill.CustomerPhone;
        if (TxtCustomerName != null) TxtCustomerName.Text = _activeBill.CustomerName;
        if (TxtCustomerStatus != null)
        {
            TxtCustomerStatus.Text = string.IsNullOrWhiteSpace(_activeBill.CustomerName)
                ? "Enter customer mobile & press Enter"
                : $"✔ Customer: {_activeBill.CustomerName}";
        }

        UpdateHeldIndicator();
        CalculateTotals();
        if (string.IsNullOrWhiteSpace(_activeBill.CustomerPhone))
            TxtCustomerPhone?.Focus();
        else
            TxtBarcodeEntry?.Focus();
    }

    private void SwitchToNextBill()
    {
        if (_openBills.Count <= 1) return;

        int currentIndex = _openBills.IndexOf(_activeBill);
        int nextIndex = (currentIndex + 1) % _openBills.Count;
        SwitchToBill(_openBills[nextIndex]);
    }

    private void UpdateHeldIndicator()
    {
        if (HeldStatusBadge == null || RowHeldCount == null) return;

        int totalBills = _openBills.Count;
        if (totalBills > 1)
        {
            int currentIdx = _openBills.IndexOf(_activeBill) + 1;
            HeldStatusBadge.Visibility = Visibility.Visible;
            RowHeldCount.Visibility = Visibility.Visible;

            TxtHeldHint.Text = $"⏸️ {totalBills - 1} on Hold | Bill {currentIdx} of {totalBills} [Press Tab to switch]";
            TxtActiveBillCount.Text = $"{currentIdx} of {totalBills} (Tab)";
        }
        else
        {
            HeldStatusBadge.Visibility = Visibility.Collapsed;
            RowHeldCount.Visibility = Visibility.Collapsed;
            if (_activeBill != null)
            {
                _activeBill.IsHold = false;
            }
        }
    }

    private void HeldStatusBadge_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        SwitchToNextBill();
    }

    private void CloseBillSession(BillSession billToClose)
    {
        if (_openBills.Count <= 1)
        {
            ResetActiveBill();
            return;
        }

        _openBills.Remove(billToClose);

        if (_activeBill == billToClose)
        {
            SwitchToBill(_openBills.Last());
        }
        else
        {
            UpdateHeldIndicator();
        }
    }

    // ════════════════════ KEYBOARD TAB SWITCHING ════════════════════

    private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // If tender payment overlay is visible, let Esc close it
        if (TenderOverlay.Visibility == Visibility.Visible)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                TenderOverlay.Hide();
                TxtBarcodeEntry.Focus();
            }
            return;
        }

        // If product overlay is visible, let Esc close it
        if (ProductOverlay.Visibility == Visibility.Visible)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                ProductOverlay.Hide();
                TxtBarcodeEntry.Focus();
            }
            return;
        }

        // If customer overlay is visible, let Esc close it
        if (CustomerOverlay.Visibility == Visibility.Visible)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                CustomerOverlay.Hide();
                TxtCustomerPhone.Focus();
            }
            return;
        }

        // If view bills overlay is visible, let Esc close it
        if (BillsOverlay.Visibility == Visibility.Visible)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                BillsOverlay.Hide();
                TxtBarcodeEntry.Focus();
            }
            return;
        }

        // Single Tab key cycles through open/held bills (when multiple bills are open)
        // Note: Ctrl+Tab is intentionally left unhandled so MainWindow cycles all app screens till HomePage!
        if (e.Key == Key.Tab && Keyboard.Modifiers == ModifierKeys.None && _openBills.Count > 1)
        {
            e.Handled = true;
            SwitchToNextBill();
        }
        else if (e.Key == Key.F1)
        {
            e.Handled = true;
            OpenProductLookup();
        }
        else if (e.Key == Key.F3)
        {
            e.Handled = true;
            SalesReturn_Click(sender, e);
        }
        else if (e.Key == Key.F5)
        {
            e.Handled = true;
            ViewBills_Click(sender, e);
        }
        else if (e.Key == Key.F10)
        {
            e.Handled = true;
            HoldBill_Click(sender, e);
        }
        else if (e.Key == Key.F11)
        {
            e.Handled = true;
            SelectCustomer_Click(sender, e);
        }
        else if (e.Key == Key.F2)
        {
            e.Handled = true;
            SaveAndPrint_Click(sender, e);
        }
        else if (e.Key == Key.F7)
        {
            e.Handled = true;
            ClearBill_Click(sender, e);
        }
        else if (e.Key == Key.F12)
        {
            e.Handled = true;
            CloseBill_Click(sender, e);
        }
    }

    // ════════════════════ DATA CALCULATIONS ════════════════════

    private static decimal ParseGstPct(string? gstText)
    {
        if (string.IsNullOrWhiteSpace(gstText)) return 0m;
        string cleaned = new string(gstText.Where(c => char.IsDigit(c) || c == '.').ToArray());
        return decimal.TryParse(cleaned, out decimal pct) ? pct : 0m;
    }

    private void AddSampleRow(string code, string item, decimal rate, int qty, decimal disc, decimal gstPct = 0m, decimal availableStock = decimal.MaxValue)
    {
        _activeBill.Items.Add(new BillRow
        {
            Code = code,
            Item = item,
            Rate = rate,
            Qty = qty,
            DiscPct = disc,
            GstPct = gstPct,
            AvailableStock = availableStock,
            SalesMan = "Nawaz"
        });
        UpdateHeldIndicator();
        CalculateTotals();
    }

    private void CalculateTotals()
    {
        if (TxtGross == null || TxtItemOffer == null || TxtSubTot == null ||
            TxtTax == null || TxtGrandTotal == null || TxtRowCount == null || TxtTotalQty == null)
            return;

        decimal gross = _activeBill.Items.Sum(r => r.Rate * r.Qty);
        decimal rawTotal = _activeBill.Items.Sum(r => r.Amount);
        decimal itemOffer = gross - rawTotal;

        // Rate is inclusive of each item's own GST %: back-calculate included GST, do NOT add GST on top
        decimal taxable = 0m;
        decimal tax = 0m;
        foreach (var r in _activeBill.Items)
        {
            decimal rowAmt = r.Amount;
            decimal rowTaxable = Math.Round(rowAmt / (1m + r.GstPct / 100m), 2);
            taxable += rowTaxable;
            tax += rowAmt - rowTaxable;
        }

        decimal sgst = Math.Round(tax / 2m, 2);
        decimal cgst = tax - sgst;

        if (TxtTaxLabel != null)
        {
            var distinctRates = _activeBill.Items.Select(r => r.GstPct).Distinct().ToList();
            if (distinctRates.Count == 1)
            {
                decimal half = distinctRates[0] / 2m;
                TxtTaxLabel.Text = $"GST ({distinctRates[0]:0.##}%)";
                if (TxtCgstLabel != null) TxtCgstLabel.Text = $"CGST ({half:0.##}%)";
                if (TxtSgstLabel != null) TxtSgstLabel.Text = $"SGST ({half:0.##}%)";
            }
            else
            {
                TxtTaxLabel.Text = "GST Tax";
                if (TxtCgstLabel != null) TxtCgstLabel.Text = "CGST";
                if (TxtSgstLabel != null) TxtSgstLabel.Text = "SGST";
            }
        }

        // Round total: <= .50 rounds down (e.g. 49.50 -> 49), >= .51 rounds up (e.g. 49.51 -> 50)
        decimal roundedTotal = rawTotal > 0m ? Math.Ceiling(rawTotal - 0.50m) : 0m;
        decimal rounding = roundedTotal - rawTotal;

        TxtGross.Text = gross.ToString("F2");
        TxtItemOffer.Text = itemOffer.ToString("F2");
        TxtSubTot.Text = taxable.ToString("F2");
        if (TxtCgst != null) TxtCgst.Text = cgst.ToString("F2");
        if (TxtSgst != null) TxtSgst.Text = sgst.ToString("F2");
        TxtTax.Text = tax.ToString("F2");
        if (TxtRounding != null)
        {
            TxtRounding.Text = rounding.ToString("+0.00;-0.00;0.00");
        }
        TxtGrandTotal.Text = roundedTotal.ToString("F2");

        TxtRowCount.Text = _activeBill.Items.Count.ToString();
        TxtTotalQty.Text = _activeBill.Items.Sum(r => r.Qty).ToString();

        UpdateChange();
    }

    private void UpdateChange()
    {
        if (TxtChangeGiven == null || TxtTendered == null || TxtGrandTotal == null)
            return;

        if (decimal.TryParse(TxtTendered.Text, out decimal tendered) &&
            decimal.TryParse(TxtGrandTotal.Text, out decimal total))
        {
            decimal change = tendered - total;
            TxtChangeGiven.Text = change > 0 ? change.ToString("F2") : "0.00";
        }
    }

    private void TxtTendered_TextChanged(object sender, TextChangedEventArgs e) => UpdateChange();

    // ════════════════════ CUSTOMER LOOKUP & ENTRY ════════════════════

    private void DigitsOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        foreach (char c in e.Text)
        {
            if (!char.IsDigit(c))
            {
                e.Handled = true;
                return;
            }
        }
    }

    private void TxtCustomerPhone_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;

        string phone = TxtCustomerPhone.Text.Trim();
        if (string.IsNullOrEmpty(phone))
        {
            // Open Customers List overlay when pressing Enter on empty mobile number
            CustomerOverlay.Show();
            return;
        }

        var existing = DatabaseService.GetCustomerByPhone(phone);
        if (existing.HasValue)
        {
            _activeBill.CustomerPhone = existing.Value.Phone;
            _activeBill.CustomerName = existing.Value.Name;
            TxtCustomerName.Text = existing.Value.Name;
            TxtCustomerStatus.Text = $"✔ Found: {existing.Value.Name} ({existing.Value.CustCode})";
            TxtBarcodeEntry.Focus();
        }
        else
        {
            _activeBill.CustomerPhone = phone;
            _activeBill.CustomerName = string.Empty;
            TxtCustomerName.Clear();
            TxtCustomerStatus.Text = "New customer — enter Name & press Enter";
            TxtCustomerName.Focus();
        }
    }

    private void TxtCustomerName_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;

        string phone = TxtCustomerPhone.Text.Trim();
        string name = TxtCustomerName.Text.Trim();
        if (string.IsNullOrEmpty(phone))
        {
            MessageBox.Show("Please enter Customer Mobile number first.", "Customer Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtCustomerPhone.Focus();
            return;
        }
        if (string.IsNullOrEmpty(name))
        {
            MessageBox.Show("Please enter Customer Name.", "Customer Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtCustomerName.Focus();
            return;
        }

        string custCode = DatabaseService.UpsertCustomer(phone, name);
        _activeBill.CustomerPhone = phone;
        _activeBill.CustomerName = name;
        TxtCustomerStatus.Text = $"✔ Saved: {name} ({custCode})";
        TxtBarcodeEntry.Focus();
    }

    private bool EnsureCustomerEntered()
    {
        string phone = TxtCustomerPhone.Text.Trim();
        string name = TxtCustomerName.Text.Trim();

        if (string.IsNullOrEmpty(phone))
        {
            MessageBox.Show("Please enter Customer Mobile number before adding items.", "Customer Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtCustomerPhone.Focus();
            return false;
        }

        if (string.IsNullOrEmpty(name))
        {
            var existing = DatabaseService.GetCustomerByPhone(phone);
            if (existing.HasValue)
            {
                name = existing.Value.Name;
                TxtCustomerName.Text = name;
                TxtCustomerStatus.Text = $"✔ Found: {name} ({existing.Value.CustCode})";
            }
            else
            {
                MessageBox.Show("Number not found in database. Please enter Customer Name before adding items.", "Customer Name Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtCustomerStatus.Text = "New customer — enter Name & press Enter";
                TxtCustomerName.Focus();
                return false;
            }
        }
        else if (_activeBill.CustomerPhone != phone || _activeBill.CustomerName != name)
        {
            string custCode = DatabaseService.UpsertCustomer(phone, name);
            TxtCustomerStatus.Text = $"✔ Saved: {name} ({custCode})";
        }

        _activeBill.CustomerPhone = phone;
        _activeBill.CustomerName = name;
        return true;
    }

    private void TxtBarcodeEntry_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            OpenProductLookup();
        }
    }

    private void AddLine_Click(object sender, RoutedEventArgs e)
    {
        OpenProductLookup();
    }

    private void AddSampleItem_Click(object sender, RoutedEventArgs e) =>
        OpenProductLookup();

    private void OpenProductLookup()
    {
        if (!EnsureCustomerEntered()) return;

        string input = TxtBarcodeEntry.Text.Trim();
        ProductOverlay.Show(input);
    }

    private void OnProductSelectedFromOverlay(ProductItem product)
    {
        int alreadyInBill = _activeBill.Items
            .Where(r => r.Code.Equals(product.Code, StringComparison.OrdinalIgnoreCase))
            .Sum(r => r.Qty);

        if (product.Stock <= 0 || alreadyInBill + 1 > product.Stock)
        {
            MessageBox.Show(
                $"Cannot add '{product.Name}'.\nAvailable Stock: {product.Stock:0.##} (Already in bill: {alreadyInBill}).",
                "Insufficient Stock",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            TxtBarcodeEntry.Focus();
            return;
        }

        decimal gstPct = ParseGstPct(product.Gst);
        AddSampleRow(product.Code, product.Name, product.Rate, 1, 0, gstPct, product.Stock);
        TxtBarcodeEntry.Clear();
        TxtBarcodeEntry.Focus();
    }

    private void GridBillItems_CellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
    {
        Dispatcher.InvokeAsync(() =>
        {
            if (e.Row.Item is BillRow row)
            {
                if (row.Qty < 1) row.Qty = 1;

                int otherQty = _activeBill.Items
                    .Where(r => r != row && r.Code.Equals(row.Code, StringComparison.OrdinalIgnoreCase))
                    .Sum(r => r.Qty);

                int maxAllowed = row.AvailableStock == decimal.MaxValue
                    ? int.MaxValue
                    : Math.Max(0, (int)Math.Floor(row.AvailableStock) - otherQty);

                if (row.Qty > maxAllowed)
                {
                    row.Qty = Math.Max(1, maxAllowed);
                    MessageBox.Show(
                        $"Cannot exceed available stock ({row.AvailableStock:0.##}) for '{row.Item}'.",
                        "Stock Limit",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    GridBillItems.Items.Refresh();
                }
            }
            CalculateTotals();
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    private void SaveAndPrint_Click(object sender, RoutedEventArgs e)
    {
        if (_activeBill.Items.Count == 0)
        {
            MessageBox.Show("Cannot save an empty bill. Please add items first.", "QuickPOS", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        decimal.TryParse(TxtGrandTotal.Text, out decimal total);
        string currentTendered = TxtTendered?.Text ?? total.ToString("F2");

        // Open Payment Tender Modal Overlay in the center of the screen
        TenderOverlay.Show(_activeBill.BillNo, total, _activeBill.Items.Count, currentTendered);
    }

    private void OnPaymentConfirmed(PaymentResult payment)
    {
        decimal.TryParse(TxtSubTot.Text, out decimal subTotal);
        decimal.TryParse(TxtTax.Text, out decimal tax);
        decimal total = payment.AmountDue;
        string tendered = payment.Tendered.ToString("F2");
        string change = payment.Change.ToString("F2");

        if (TxtTendered != null) TxtTendered.Text = tendered;
        if (TxtChangeGiven != null) TxtChangeGiven.Text = change;
        _activeBill.TenderedText = tendered;

        var lineItems = _activeBill.Items.Select(r => (r.Code, r.Item, r.Qty, r.Rate, r.Amount));

        string customerName = !string.IsNullOrWhiteSpace(_activeBill.CustomerName)
            ? _activeBill.CustomerName
            : TxtCustomerName.Text.Trim();
        if (string.IsNullOrWhiteSpace(customerName)) customerName = "Walk-in Customer";

        // Deduct sold quantities from SQLite product stock
        foreach (var item in _activeBill.Items)
        {
            DatabaseService.DeductStock(item.Code, item.Qty);
        }

        // Send to physical default printer if printing requested
        string defaultPrinter = ThermalPrinterService.GetDefaultPrinterName();

        // Save bill in SQLite & generate tender-prefixed Bill No (CA1, CC1, WL1...)
        var savedItems = _activeBill.Items.Select(r => new SavedBillItem
        {
            Code = r.Code,
            Item = r.Item,
            Rate = r.Rate,
            Qty = r.Qty,
            DiscPct = r.DiscPct
        });
        string custCode = !string.IsNullOrWhiteSpace(_activeBill.CustomerPhone)
            ? (DatabaseService.GetCustomerByPhone(_activeBill.CustomerPhone)?.CustCode ?? "1")
            : "1";
        bool willPrint = payment.ShouldPrint && !string.IsNullOrEmpty(defaultPrinter);
        string finalBillNo = BillsOverlay.AddBillRecord(_activeBill.BillNo, total, customerName, willPrint, payment.TenderType, savedItems, payment.Rrn, custCode);

        // Build actual ESC/POS binary data and receipt text with the final bill number
        var (escPosBytes, receiptText) = ThermalPrinterService.BuildReceipt(
            "WHITE TAG",
            finalBillNo,
            lineItems,
            subTotal,
            tax,
            total,
            tendered,
            change,
            payment.TenderType,
            payment.Rrn,
            customerName);

        // Save a digital copy in Documents\QuickPOS_Receipts
        string receiptFile = string.Empty;
        try
        {
            string receiptsDir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "QuickPOS_Receipts");
            System.IO.Directory.CreateDirectory(receiptsDir);
            string safeBillNo = finalBillNo.Replace("#", "").Replace("-", "_");
            receiptFile = System.IO.Path.Combine(receiptsDir, $"Receipt_{safeBillNo}_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            System.IO.File.WriteAllText(receiptFile, receiptText);
        }
        catch { }

        bool printed = false;
        if (willPrint)
        {
            printed = ThermalPrinterService.PrintRawToPrinter(defaultPrinter, escPosBytes);
        }

        string rrnMsg = string.IsNullOrWhiteSpace(payment.Rrn) ? "" : $"\nRRN / Ref: {payment.Rrn}";

        if (payment.ShouldPrint)
        {
            if (printed)
            {
                MessageBox.Show(
                    $"Bill {finalBillNo} Saved!\nPayment: {payment.TenderType} (Rs. {total:F2}){rrnMsg}\nReceipt printed on: {defaultPrinter}!",
                    "Bill Saved & Receipt Printed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(
                    $"Bill {finalBillNo} Saved!\nPayment: {payment.TenderType} (Rs. {total:F2}){rrnMsg}\nReceipt saved to:\n{receiptFile}",
                    "Bill Saved",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        else
        {
            MessageBox.Show(
                $"Bill {finalBillNo} Saved successfully [No Print]!\nPayment: {payment.TenderType} (Rs. {total:F2}){rrnMsg}\nReceipt copy saved to:\n{receiptFile}",
                "Bill Saved [No Print]",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // Close finished bill
        CloseBillSession(_activeBill);
    }

    private void ViewBills_Click(object sender, RoutedEventArgs e)
    {
        BillsOverlay.Show();
    }

    private void SalesReturn_Click(object sender, RoutedEventArgs e)
    {
        ReturnRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ClearBill_Click(object sender, RoutedEventArgs e)
    {
        if (_activeBill.Items.Count > 0)
        {
            var result = MessageBox.Show(
                "Are you sure you want to clear all items from this bill?",
                "Clear Bill",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;
        }

        ResetActiveBill(clearCustomer: false);
    }

    private void ResetActiveBill(bool clearCustomer = true)
    {
        _activeBill.Items.Clear();
        if (clearCustomer)
        {
            _activeBill.Id = DatabaseService.GetNextSalesBillNumber();
            if (TxtBillNo != null) TxtBillNo.Text = _activeBill.BillNo;
            _activeBill.CustomerPhone = string.Empty;
            _activeBill.CustomerName = string.Empty;
            if (TxtCustomerPhone != null) TxtCustomerPhone.Clear();
            if (TxtCustomerName != null) TxtCustomerName.Clear();
            if (TxtCustomerStatus != null) TxtCustomerStatus.Text = "Enter customer mobile & press Enter";
        }
        if (TxtTendered != null) TxtTendered.Text = "0.00";
        if (TxtChangeGiven != null) TxtChangeGiven.Text = "0.00";
        UpdateHeldIndicator();
        CalculateTotals();

        if (clearCustomer)
            TxtCustomerPhone?.Focus();
        else
            TxtBarcodeEntry?.Focus();
    }

    private void CloseBill_Click(object sender, RoutedEventArgs e)
    {
        if (_openBills.Count > 1)
        {
            // If the bill being closed has items, confirm with user
            if (_activeBill.Items.Count > 0)
            {
                var result = MessageBox.Show(
                    $"Close Bill {_activeBill.BillNo} with {_activeBill.Items.Count} item(s)?\nThe other open bill will remain intact.",
                    "Close Current Bill",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes) return;
            }

            // Close ONLY this bill session, and stay on the remaining bill!
            CloseBillSession(_activeBill);
        }
        else
        {
            // Only 1 bill remaining: clear all (items + customer) without saving, then close to Dashboard
            if (_activeBill.Items.Count > 0)
            {
                var result = MessageBox.Show(
                    "This bill has items added. Are you sure you want to close Sales Bill without saving?",
                    "Close Sales Bill",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes) return;
            }

            ResetActiveBill(clearCustomer: true);
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void SelectCustomer_Click(object sender, RoutedEventArgs e)
    {
        CustomerOverlay.Show();
    }

    private void OnCustomerSelectedFromOverlay(CustomerRecord cust)
    {
        _activeBill.CustomerPhone = cust.Phone;
        _activeBill.CustomerName = cust.Name;
        TxtCustomerPhone.Text = cust.Phone;
        TxtCustomerName.Text = cust.Name;
        TxtCustomerStatus.Text = $"✔ Imported: {cust.Name} ({cust.CustCode})";
        TxtBarcodeEntry.Focus();
    }

    private void FKey_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            MessageBox.Show($"Executed command: {btn.Tag}", "Function Key", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
