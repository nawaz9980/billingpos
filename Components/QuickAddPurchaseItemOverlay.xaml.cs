using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using QuickPos.Services;

namespace QuickPos.Components;

public sealed class VariantMatrixItem : INotifyPropertyChanged
{
    private bool _isIncluded = true;
    public string Style { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;

    public bool IsIncluded
    {
        get => _isIncluded;
        set
        {
            _isIncluded = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsIncluded)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class VariantMatrixRow
{
    public string StyleDisplay { get; set; } = string.Empty;
    public List<VariantMatrixItem> Cells { get; set; } = [];
}

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
    private readonly ObservableCollection<VariantMatrixItem> _variantMatrix = [];
    private readonly ObservableCollection<VariantMatrixRow> _matrixRows = [];
    private List<string> _selectedStyles = [];
    private List<string> _selectedSizes = [];

    public QuickAddPurchaseItemOverlay()
    {
        InitializeComponent();
        GridVariantMatrix.ItemsSource = _matrixRows;

        _tabOrder =
        [
            TxtItemName, TxtHsnCode, CmbGst, TxtBrand, TxtStyle, TxtSize, TxtBarcode, BtnAdd
        ];

        MasterSideOverlay.ItemsSelected += OnMasterItemsSelected;
        MasterSideOverlay.CloseRequested += OnMasterOverlayClosed;
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
        _selectedStyles = [];
        _selectedSizes = [];
        _variantMatrix.Clear();
        _matrixRows.Clear();
        VariantMatrixOverlay.Visibility = Visibility.Collapsed;
        MasterSideOverlay.Hide();

        Visibility = Visibility.Visible;
        Dispatcher.InvokeAsync(() => TxtItemName.Focus(), System.Windows.Threading.DispatcherPriority.Input);
    }

    public void Hide()
    {
        VariantMatrixOverlay.Visibility = Visibility.Collapsed;
        MasterSideOverlay.Hide();
        Visibility = Visibility.Collapsed;
    }

    private void OnMasterItemsSelected(string tableName, List<string> values)
    {
        if (tableName == "Brands")
        {
            TxtBrand.Text = values.FirstOrDefault() ?? string.Empty;
            TxtStyle.Focus();
        }
        else if (tableName == "Styles")
        {
            _selectedStyles = values;
            TxtStyle.Text = string.Join(", ", values);
            if (_selectedStyles.Count > 1 || _selectedSizes.Count > 1)
                TxtBarcode.Text = string.Empty;
            RebuildVariantMatrix(openOverlay: false);
            TxtSize.Focus();
        }
        else if (tableName == "Sizes")
        {
            _selectedSizes = values;
            TxtSize.Text = string.Join(", ", values);
            if (_selectedStyles.Count > 1 || _selectedSizes.Count > 1)
                TxtBarcode.Text = string.Empty;
            RebuildVariantMatrix(openOverlay: true);
            if (VariantMatrixOverlay.Visibility != Visibility.Visible)
                TxtBarcode.Focus();
        }
    }

    private void RebuildVariantMatrix(bool openOverlay)
    {
        _variantMatrix.Clear();
        _matrixRows.Clear();
        GridVariantMatrix.Columns.Clear();

        var styles = _selectedStyles.Count > 0 ? _selectedStyles : [string.Empty];
        var sizes = _selectedSizes.Count > 0 ? _selectedSizes : [string.Empty];

        if (styles.Count * sizes.Count <= 1)
        {
            VariantMatrixOverlay.Visibility = Visibility.Collapsed;
            return;
        }

        // First column: Style
        GridVariantMatrix.Columns.Add(new DataGridTextColumn
        {
            Header = "Style",
            Binding = new Binding(nameof(VariantMatrixRow.StyleDisplay)),
            IsReadOnly = true,
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });

        // One CheckBox column per Size
        for (int colIdx = 0; colIdx < sizes.Count; colIdx++)
        {
            string szHeader = string.IsNullOrEmpty(sizes[colIdx]) ? "Select" : sizes[colIdx];
            var factory = new FrameworkElementFactory(typeof(CheckBox));
            factory.SetBinding(CheckBox.IsCheckedProperty, new Binding($"Cells[{colIdx}].IsIncluded")
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });
            factory.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
            factory.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            factory.AddHandler(CheckBox.ClickEvent, new RoutedEventHandler((_, _) => UpdateMatrixCount()));

            GridVariantMatrix.Columns.Add(new DataGridTemplateColumn
            {
                Header = szHeader,
                Width = new DataGridLength(62),
                CellTemplate = new DataTemplate { VisualTree = factory }
            });
        }

        foreach (string st in styles)
        {
            var row = new VariantMatrixRow
            {
                StyleDisplay = string.IsNullOrEmpty(st) ? "(Default)" : st
            };

            foreach (string sz in sizes)
            {
                var cell = new VariantMatrixItem
                {
                    Style = st,
                    Size = sz,
                    IsIncluded = true
                };
                row.Cells.Add(cell);
                _variantMatrix.Add(cell);
            }

            _matrixRows.Add(row);
        }

        UpdateMatrixCount();
        if (openOverlay)
            VariantMatrixOverlay.Visibility = Visibility.Visible;
    }

    private void UpdateMatrixCount()
    {
        int active = _variantMatrix.Count(v => v.IsIncluded);
        TxtMatrixCount.Text = $"{active}/{_variantMatrix.Count} active";
    }

    private void MatrixTickAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in _variantMatrix) item.IsIncluded = true;
        UpdateMatrixCount();
    }

    private void MatrixUntickAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in _variantMatrix) item.IsIncluded = false;
        UpdateMatrixCount();
    }

    private void CloseMatrix_Click(object sender, RoutedEventArgs e)
    {
        VariantMatrixOverlay.Visibility = Visibility.Collapsed;
        TxtBarcode.Focus();
    }

    private void OnMasterOverlayClosed(string tableName)
    {
        if (tableName == "Brands") TxtBrand.Focus();
        else if (tableName == "Styles") TxtStyle.Focus();
        else if (tableName == "Sizes") TxtSize.Focus();
    }

    private void OpenBrandOverlay_Click(object sender, RoutedEventArgs e)
    {
        MasterSideOverlay.Show("Brands", "Brands List", multiSelect: false, initialFilter: TxtBrand.Text.Trim());
    }

    private void OpenStyleOverlay_Click(object sender, RoutedEventArgs e)
    {
        MasterSideOverlay.Show("Styles", "Styles List (Multi-Select)", multiSelect: true, preselected: _selectedStyles);
    }

    private void OpenSizeOverlay_Click(object sender, RoutedEventArgs e)
    {
        MasterSideOverlay.Show("Sizes", "Sizes List (Multi-Select)", multiSelect: true, preselected: _selectedSizes);
    }

    private void TxtBrand_MouseDown(object sender, MouseButtonEventArgs e)
    {
        OpenBrandOverlay_Click(this, new RoutedEventArgs());
        e.Handled = true;
    }

    private void TxtStyle_MouseDown(object sender, MouseButtonEventArgs e)
    {
        OpenStyleOverlay_Click(this, new RoutedEventArgs());
        e.Handled = true;
    }

    private void TxtSize_MouseDown(object sender, MouseButtonEventArgs e)
    {
        OpenSizeOverlay_Click(this, new RoutedEventArgs());
        e.Handled = true;
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
            MessageBox.Show("Please enter * Item Name.", "Quick Item Creation", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtItemName.Focus();
            return;
        }

        string hsn = TxtHsnCode.Text.Trim();
        string brand = TxtBrand.Text.Trim();
        string gst = (CmbGst.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "5%";

        if (!string.IsNullOrEmpty(brand))
            DatabaseService.AddMasterName("Brands", brand);

        var stylesToCreate = _selectedStyles.Count > 0
            ? _selectedStyles
            : (string.IsNullOrWhiteSpace(TxtStyle.Text) ? [string.Empty] : [TxtStyle.Text.Trim()]);

        foreach (string st in stylesToCreate.Where(s => !string.IsNullOrEmpty(s)))
            DatabaseService.AddMasterName("Styles", st);

        var sizesToCreate = _selectedSizes.Count > 0 ? _selectedSizes : [string.Empty];

        int totalVariants = _variantMatrix.Count > 0
            ? _variantMatrix.Count(v => v.IsIncluded)
            : stylesToCreate.Count * sizesToCreate.Count;

        if (totalVariants == 0)
        {
            MessageBox.Show("Please tick at least one Style × Size combination.", "Quick Item Creation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var combinations = _variantMatrix.Count > 0
            ? _variantMatrix.Where(v => v.IsIncluded).Select(v => (v.Style, v.Size)).ToList()
            : stylesToCreate.SelectMany(st => sizesToCreate.Select(sz => (st, sz))).ToList();

        var created = new List<QuickAddedPurchaseItem>();

        foreach (var (st, sz) in combinations)
        {
            string code = DatabaseService.GetNextProductCode();
            string barcode = totalVariants == 1 && !string.IsNullOrWhiteSpace(TxtBarcode.Text)
                ? TxtBarcode.Text.Trim()
                : DatabaseService.GetNextInternalBarcode();

            string nameWithStyle = stylesToCreate.Count > 1 && !string.IsNullOrWhiteSpace(st)
                ? $"{baseName} - {st}"
                : baseName;
            string itemName = string.IsNullOrWhiteSpace(sz) ? nameWithStyle : $"{nameWithStyle} ({sz})";
            string alias = string.IsNullOrWhiteSpace(sz) ? barcode : $"{sz}-{barcode}";

            DatabaseService.AddProduct(code, itemName, alias, 0m, 0m, gst, barcode, sz, hsn, brand, st);

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
                    Style = st,
                    Stock = 0m,
                    Rate = 0m,
                    Gst = gst
                },
                1,
                0m,
                0m,
                0m));
        }

        Hide();
        ItemsCreated?.Invoke(created);
    }

    private void Clear_Click(object sender, RoutedEventArgs e) => Show();

    private void Help_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "Quick Item Creation:\n" +
            "• Enter : Move to next field\n" +
            "• Brand / Style / Size : Press Enter or click Select to open bottom-right list\n" +
            "• Style & Size : Tick multiple options (or + Add new)\n" +
            "• Barcode : Scan branded barcode or leave blank to auto-generate\n" +
            "• F6 : Save item(s) & add to Purchase Invoice\n" +
            "• F7 : Clear form\n" +
            "• F12 / Esc : Close",
            "Quick Item Creation - Help", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        CloseRequested?.Invoke();
    }

    private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (VariantMatrixOverlay.Visibility == Visibility.Visible)
        {
            if (e.Key == Key.Escape || e.Key == Key.Enter)
            {
                e.Handled = true;
                CloseMatrix_Click(this, new RoutedEventArgs());
            }
            return;
        }

        if (MasterSideOverlay.Visibility == Visibility.Visible)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                MasterSideOverlay.Hide();
            }
            return;
        }

        if (e.Key == Key.Escape || e.Key == Key.F12)
        {
            e.Handled = true;
            Close_Click(this, new RoutedEventArgs());
        }
        else if (e.Key == Key.F1)
        {
            e.Handled = true;
            Help_Click(this, new RoutedEventArgs());
        }
        else if (e.Key == Key.F6)
        {
            e.Handled = true;
            Add_Click(this, new RoutedEventArgs());
        }
        else if (e.Key == Key.F7)
        {
            e.Handled = true;
            Clear_Click(this, new RoutedEventArgs());
        }
        else if (e.Key == Key.Enter)
        {
            var focused = Keyboard.FocusedElement as DependencyObject;
            if (focused == null) return;

            if (IsInside(focused, TxtBrand))
            {
                e.Handled = true;
                OpenBrandOverlay_Click(this, new RoutedEventArgs());
                return;
            }
            if (IsInside(focused, TxtStyle))
            {
                e.Handled = true;
                OpenStyleOverlay_Click(this, new RoutedEventArgs());
                return;
            }
            if (IsInside(focused, TxtSize))
            {
                e.Handled = true;
                OpenSizeOverlay_Click(this, new RoutedEventArgs());
                return;
            }

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
