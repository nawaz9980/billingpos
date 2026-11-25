using System.Windows;
using System.Windows.Controls;
using QuickPos.Services;

namespace QuickPos.Screens;

public partial class ProductsScreen : UserControl
{
    public ProductsScreen()
    {
        InitializeComponent();
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        LoadProducts();
    }

    private void LoadProducts()
    {
        GridCatalog.ItemsSource = DatabaseService.GetProducts();
    }

    private void AddProduct_Click(object sender, RoutedEventArgs e)
    {
        string code = TxtCode.Text.Trim();
        string name = TxtName.Text.Trim();
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(name))
        {
            MessageBox.Show("Please enter both Product Code and Name.", "Products", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!decimal.TryParse(TxtRate.Text.Trim(), out decimal rate) || rate < 0)
        {
            MessageBox.Show("Please enter a valid Rate.", "Products", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        decimal.TryParse(TxtStock.Text.Trim(), out decimal stock);
        string alias = TxtAlias.Text.Trim();
        string gst = string.IsNullOrWhiteSpace(TxtGst.Text) ? "8%" : TxtGst.Text.Trim();

        DatabaseService.AddProduct(code, name, alias, stock, rate, gst);

        TxtCode.Clear();
        TxtName.Clear();
        TxtAlias.Clear();
        TxtStock.Text = "0";
        TxtRate.Clear();
        TxtCode.Focus();

        LoadProducts();
    }
}
