using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace QuickPos.Components;

public class ProductItem
{
    public int Sno { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Alias { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public string HsnCode { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Style { get; set; } = string.Empty;
    public decimal Stock { get; set; }
    public decimal Rate { get; set; }
    public string Gst { get; set; } = "12%";
}

public partial class ProductLookupOverlay : UserControl
{
    private readonly List<ProductItem> _allProducts = [];
    private List<ProductItem> _filteredProducts = [];

    public event Action<ProductItem>? ProductSelected;
    public event Action? CloseRequested;

    public ProductLookupOverlay()
    {
        InitializeComponent();
        LoadCatalogFromDb();
        ApplyFilter(string.Empty);
    }

    private void LoadCatalogFromDb()
    {
        _allProducts.Clear();
        _allProducts.AddRange(QuickPos.Services.DatabaseService.GetProducts());
    }

    public void Show(string initialSearch = "")
    {
        LoadCatalogFromDb();
        Visibility = Visibility.Visible;
        TxtSearch.Text = initialSearch;
        TxtSearch.SelectAll();
        ApplyFilter(initialSearch);
        TxtSearch.Focus();
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
            _filteredProducts = [.. _allProducts];
        }
        else
        {
            _filteredProducts = _allProducts.Where(p =>
                p.Name.ToLowerInvariant().Contains(query) ||
                p.Code.ToLowerInvariant().Contains(query) ||
                p.Alias.ToLowerInvariant().Contains(query) ||
                p.Barcode.ToLowerInvariant().Contains(query) ||
                p.Size.ToLowerInvariant().Contains(query) ||
                p.HsnCode.ToLowerInvariant().Contains(query)
            ).ToList();
        }

        GridProducts.ItemsSource = _filteredProducts;

        if (_filteredProducts.Count > 0)
        {
            GridProducts.SelectedIndex = 0;
            GridProducts.ScrollIntoView(_filteredProducts[0]);
        }

        UpdateRecordsCount();
    }

    private void UpdateRecordsCount()
    {
        int sel = GridProducts.SelectedIndex >= 0 ? GridProducts.SelectedIndex + 1 : (_filteredProducts.Count > 0 ? 1 : 0);
        TxtRecordsCount.Text = $"Records : {sel}/{_filteredProducts.Count}";
    }

    private void ConfirmSelection()
    {
        var selected = GridProducts.SelectedItem as ProductItem;
        if (selected == null && _filteredProducts.Count > 0)
        {
            selected = _filteredProducts[0];
        }

        if (selected != null)
        {
            Hide();
            ProductSelected?.Invoke(selected);
        }
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter(TxtSearch.Text);
    }

    private void TxtSearch_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down)
        {
            e.Handled = true;
            if (GridProducts.SelectedIndex < _filteredProducts.Count - 1)
            {
                GridProducts.SelectedIndex++;
                GridProducts.ScrollIntoView(GridProducts.SelectedItem);
                UpdateRecordsCount();
            }
        }
        else if (e.Key == Key.Up)
        {
            e.Handled = true;
            if (GridProducts.SelectedIndex > 0)
            {
                GridProducts.SelectedIndex--;
                GridProducts.ScrollIntoView(GridProducts.SelectedItem);
                UpdateRecordsCount();
            }
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            ConfirmSelection();
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close_Click(sender, e);
        }
    }

    private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            ConfirmSelection();
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close_Click(sender, e);
        }
    }

    private void GridProducts_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Prevent default DataGrid behavior of moving down a row when Enter is pressed
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            ConfirmSelection();
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close_Click(sender, e);
        }
    }

    private void GridProducts_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateRecordsCount();
    }

    private void GridProducts_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ConfirmSelection();
    }

    private void SelectProduct_Click(object sender, RoutedEventArgs e)
    {
        ConfirmSelection();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        CloseRequested?.Invoke();
    }
}
