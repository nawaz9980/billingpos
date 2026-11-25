using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace QuickPos.Components;

public class PaymentResult
{
    public string TenderType { get; set; } = "Cash";
    public decimal AmountDue { get; set; }
    public decimal Tendered { get; set; }
    public decimal Change { get; set; }
    public string Rrn { get; set; } = string.Empty;
    public bool ShouldPrint { get; set; } = true;
}

public partial class PaymentTenderOverlay : UserControl
{
    private decimal _totalDue;
    private bool _returnMode;
    private readonly SolidColorBrush _redBrush = new(Color.FromRgb(239, 68, 68));
    private readonly SolidColorBrush _greenBrush = new(Color.FromRgb(34, 197, 94));

    public event Action<PaymentResult>? PaymentConfirmed;
    public event Action? CancelRequested;

    public PaymentTenderOverlay()
    {
        InitializeComponent();
    }

    public void Show(string billNo, decimal totalDue, int itemCount, string initialTendered = "", bool returnMode = false)
    {
        _totalDue = totalDue;
        _returnMode = returnMode;

        // Toggle UPI & Debit Card visibility (Return mode shows only Cash and RRN)
        var extraVis = returnMode ? Visibility.Collapsed : Visibility.Visible;
        var extraHeight = returnMode ? new GridLength(0) : new GridLength(38);
        RowDefUpi.Height = extraHeight;
        RowDefDebitCard.Height = extraHeight;
        CellUpiLabel.Visibility = extraVis;
        CellUpiInput.Visibility = extraVis;
        CellDebitLabel.Visibility = extraVis;
        CellDebitInput.Visibility = extraVis;
        LblRrn.Text = returnMode ? "B. RRN" : "D. RRN";

        TxtSummaryTotal.Text = totalDue.ToString("F2");
        TxtCash.Text = string.Empty;
        TxtUpi.Text = string.Empty;
        TxtDebitCard.Text = string.Empty;
        TxtRrn.Text = string.Empty;

        RecalculateTotals();

        Visibility = Visibility.Visible;
        TxtCash.Focus();
    }

    public void Hide()
    {
        Visibility = Visibility.Collapsed;
    }

    private void Amount_TextChanged(object sender, TextChangedEventArgs e)
    {
        RecalculateTotals();
    }

    private void RecalculateTotals()
    {
        if (TxtSummaryTendered == null || TxtSummaryBalance == null) return;

        decimal.TryParse(TxtCash?.Text, out decimal cash);
        decimal.TryParse(TxtUpi?.Text, out decimal upi);
        decimal.TryParse(TxtDebitCard?.Text, out decimal debitCard);

        decimal totalTendered = cash + upi + debitCard;
        TxtSummaryTendered.Text = totalTendered.ToString("F2");

        decimal balance = _totalDue - totalTendered;
        if (balance <= 0)
        {
            TxtSummaryBalance.Foreground = _greenBrush;
            decimal change = Math.Abs(balance);
            TxtSummaryBalance.Text = change > 0 ? $"{change:F2} (Chg)" : "0.00";
        }
        else
        {
            TxtSummaryBalance.Foreground = _redBrush;
            TxtSummaryBalance.Text = balance.ToString("F2");
        }
    }

    private void ConfirmPayment(bool shouldPrint)
    {
        decimal.TryParse(TxtCash?.Text, out decimal cash);
        decimal.TryParse(TxtUpi?.Text, out decimal upi);
        decimal.TryParse(TxtDebitCard?.Text, out decimal debitCard);

        decimal totalTendered = cash + upi + debitCard;
        decimal change = totalTendered > _totalDue ? totalTendered - _totalDue : 0;

        // Determine primary tender mode for display / receipt
        string mode = "Cash";
        if (upi > 0) mode = "UPI";
        else if (debitCard > 0) mode = "Debit Card";

        var result = new PaymentResult
        {
            TenderType = mode,
            AmountDue = _totalDue,
            Tendered = totalTendered,
            Change = change,
            Rrn = TxtRrn?.Text.Trim() ?? string.Empty,
            ShouldPrint = shouldPrint
        };

        Hide();
        PaymentConfirmed?.Invoke(result);
    }

    private void SaveWithPrint_Click(object sender, RoutedEventArgs e)
    {
        ConfirmPayment(shouldPrint: true);
    }

    private void SaveNoPrint_Click(object sender, RoutedEventArgs e)
    {
        ConfirmPayment(shouldPrint: false);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        CancelRequested?.Invoke();
    }

    private void DecimalInput_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not TextBox tb) return;
        foreach (char c in e.Text)
        {
            if (char.IsDigit(c)) continue;
            if (c == '.' && !tb.Text.Contains('.')) continue;
            e.Handled = true;
            return;
        }
    }

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

    private void DecimalInput_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.DataObject.GetDataPresent(DataFormats.Text) ||
            e.DataObject.GetData(DataFormats.Text) is not string text ||
            !decimal.TryParse(text, out decimal val) || val < 0)
        {
            e.CancelCommand();
        }
    }

    private void DigitsOnly_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.DataObject.GetDataPresent(DataFormats.Text) ||
            e.DataObject.GetData(DataFormats.Text) is not string text)
        {
            e.CancelCommand();
            return;
        }

        foreach (char c in text)
        {
            if (!char.IsDigit(c))
            {
                e.CancelCommand();
                return;
            }
        }
    }

    private void TenderInput_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            SaveWithPrint_Click(sender, e);
        }
        else if (e.Key == Key.Down || e.Key == Key.Up)
        {
            e.Handled = true;
            TextBox[] fields = _returnMode
                ? [TxtCash, TxtRrn]
                : [TxtCash, TxtUpi, TxtDebitCard, TxtRrn];
            int idx = Array.IndexOf(fields, sender as TextBox);
            if (idx >= 0)
            {
                int nextIdx = e.Key == Key.Down
                    ? (idx + 1) % fields.Length
                    : (idx - 1 + fields.Length) % fields.Length;
                fields[nextIdx].Focus();
                fields[nextIdx].SelectAll();
            }
        }
    }

    private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Cancel_Click(sender, e);
        }
    }
}
