using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using QuickPos.Services;

namespace QuickPos.Components;

public readonly record struct QuickAddedPurchaseItem(
    ProductItem Product,
    int Qty,
    decimal PurRate,
    decimal Mrp,
    decimal Selling);

public partial class QuickAddPurchaseItemOverlay : UserControl
{
    public event Action<List<QuickAddedPurchaseItem>>? ItemsCreated;
    public event Action? CloseRequested;

    private readonly Control[] _tabOrder;

    public QuickAddPurchaseItemOverlay()
    {
        InitializeComponent();
        _tabOrder =
        [
            TxtItemName, TxtHsnCode, CmbGst, TxtBrand, TxtStyle, TxtSize, TxtBarcode, TxtQty, TxtPurRate, TxtMrp, TxtSelling, BtnAdd
        ];
    }

    public void Show(string initialBarcode = "")
    {
        TxtItemName.Text = string.Empty;
        TxtHsnCode.Text = "6205";
        CmbGst.SelectedIndex = 0;
        TxtBrand.Text = string.Empty;
        TxtStyle.Text = string.Empty;
        TxtSize.Text = string.Empty;
        TxtBarcode.Text = initialBarcode.Trim();
        TxtQty.Text = "1";
        TxtPurRate.Text = "0.00";
        TxtMrp.Text = "0.00";
        TxtSelling.Text = "0.00";

        foreach (var chk in SizeChecksPanel.Children.OfType<CheckBox>())
            chk.IsChecked = false;

        Visibility = Visibility.Visible;
        Dispatcher.InvokeAsync(() => TxtItemName.Focus(), System.Windows.Threading.DispatcherPriority.Input);
    }

    public void Hide()
    {
        Visibility = Visibility.Collapsed;
    }

    private void SizeCheck_Changed(object sender, RoutedEventArgs e)
    {
        bool anyChecked = SizeChecksPanel.Children.OfType<CheckBox>().Any(c => c.IsChecked == true);
        TxtSize.IsEnabled = !anyChecked;
        if (anyChecked)
            TxtBarcode.Text = string.Empty; // Each size gets its own auto barcode
    }

    private void AutoGenerateBarcode_Click(object sender, RoutedEventArgs e)
    {
        TxtBarcode.Text = DatabaseService.GetNextInternalBarcode();
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        string baseName = TxtItemName.Text.Trim();
        if (string.IsNullOrEmpty(baseName))
        {
            MessageBox.Show("Please enter Item Name.", "Quick Add Item", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtItemName.Focus();
            return;
        }

        if (!int.TryParse(TxtQty.Text.Trim(), out int qty) || qty <= 0)
        {
            MessageBox.Show("Please enter a valid Qty (> 0).", "Quick Add Item", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtQty.Focus();
            return;
        }

        decimal.TryParse(TxtPurRate.Text.Trim(), out decimal purRate);
        decimal.TryParse(TxtMrp.Text.Trim(), out decimal mrp);
        decimal.TryParse(TxtSelling.Text.Trim(), out decimal selling);
        if (selling <= 0) selling = mrp > 0 ? mrp : purRate;
        if (mrp <= 0) mrp = selling;

        string hsn = TxtHsnCode.Text.Trim();
        string brand = TxtBrand.Text.Trim();
        string style = TxtStyle.Text.Trim();
        string gst = (CmbGst.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "5%";

        var checkedSizes = SizeChecksPanel.Children.OfType<CheckBox>()
            .Where(c => c.IsChecked == true)
            .Select(c => c.Content?.ToString() ?? string.Empty)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        var sizesToCreate = checkedSizes.Count > 0
            ? checkedSizes
            : [TxtSize.Text.Trim()];

        var created = new List<QuickAddedPurchaseItem>();

        foreach (string sz in sizesToCreate)
        {
            string code = DatabaseService.GetNextProductCode();
            string barcode = sizesToCreate.Count == 1 && !string.IsNullOrWhiteSpace(TxtBarcode.Text)
                ? TxtBarcode.Text.Trim()
                : DatabaseService.GetNextInternalBarcode();

            string itemName = string.IsNullOrWhiteSpace(sz) ? baseName : $"{baseName} ({sz})";
            string alias = string.IsNullOrWhiteSpace(sz) ? barcode : $"{sz}-{barcode}";

            // Stock starts at 0 here because Purchase Invoice F6 Save adds Qty to stock
            DatabaseService.AddProduct(code, itemName, alias, 0m, selling, gst, barcode, sz, hsn, brand, style);

            created.Add(new QuickAddedPurchaseItem(
                new ProductItem
                {
                    Code = code,
                    Name = itemName,
                    Alias = alias,
                    Barcode = barcode,
                    Size = sz,
                    HsnCode = hsn,
                    Brand = brand,
                    Style = style,
                    Stock = 0m,
                    Rate = selling,
                    Gst = gst
                },
                qty,
                purRate,
                mrp,
                selling));
        }

        Hide();
        ItemsCreated?.Invoke(created);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        CloseRequested?.Invoke();
    }

    private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close_Click(this, new RoutedEventArgs());
        }
        else if (e.Key == Key.F6)
        {
            e.Handled = true;
            Add_Click(this, new RoutedEventArgs());
        }
        else if (e.Key == Key.Enter)
        {
            var focused = Keyboard.FocusedElement as DependencyObject;
            if (focused == null) return;

            for (int i = 0; i < _tabOrder.Length - 1; i++)
            {
                if (IsInside(focused, _tabOrder[i]))
                {
                    e.Handled = true;
                    _tabOrder[i + 1].Focus();
                    if (_tabOrder[i + 1] is TextBox tb) tb.SelectAll();
                    return;
                }
            }
        }
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
