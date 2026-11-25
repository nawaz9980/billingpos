using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Data.Sqlite;
using QuickPos.Services;

namespace QuickPos.Screens;

public partial class DistributorsScreen : UserControl
{
    public event EventHandler? CloseRequested;

    private int _editingId;
    private Control[] _detailsTabOrder = [];

    public DistributorsScreen()
    {
        InitializeComponent();

        _detailsTabOrder =
        [
            TxtCode, TxtName, TxtShortName, TxtLocation, TxtLandmark, TxtCity,
            CmbState, CmbCountry, TxtPinCode, TxtTelOffice, TxtPhone, TxtEmail,
            TxtFax, CmbType, CmbCurrency, TxtTelRes, TxtDistributorId, TxtArea, TxtBarcodeAlias
        ];

        DistLookupOverlay.DistributorSelected += LoadForEdit;
        DistLookupOverlay.CloseRequested += () => TxtName.Focus();
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateCount();
        if (_editingId == 0 && string.IsNullOrWhiteSpace(TxtCode.Text))
            ResetForm();
    }

    private void UpdateCount()
    {
        int count = DatabaseService.GetDistributors().Count;
        TxtTotalCount.Text = $"Total Distributors: {count}";
    }

    private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DistLookupOverlay.Visibility == Visibility.Visible)
        {
            if (e.Key == Key.Escape)
            {
                DistLookupOverlay.Hide();
                TxtName.Focus();
                e.Handled = true;
            }
            return;
        }

        switch (e.Key)
        {
            case Key.F1:
                Help_Click(this, new RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.F3:
                New_Click(this, new RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.F4:
            case Key.F5:
                EditPick_Click(this, new RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.F6:
                Save_Click(this, new RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.F7:
                Clear_Click(this, new RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.F8:
                Skip_Click(this, new RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.F9:
                Default_Click(this, new RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.F10:
                Delete_Click(this, new RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.F11:
                Comments_Click(this, new RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.F12:
                CloseRequested?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
                break;
            case Key.Enter:
                if (HandleEnterNavigation())
                    e.Handled = true;
                break;
        }
    }

    private bool HandleEnterNavigation()
    {
        var focused = Keyboard.FocusedElement as DependencyObject;
        if (focused == null) return false;

        // Details tab order
        for (int i = 0; i < _detailsTabOrder.Length; i++)
        {
            if (IsInside(focused, _detailsTabOrder[i]))
            {
                if (i + 1 < _detailsTabOrder.Length)
                {
                    _detailsTabOrder[i + 1].Focus();
                    if (_detailsTabOrder[i + 1] is TextBox tb) tb.SelectAll();
                }
                else
                {
                    MainTabs.SelectedIndex = 1;
                    Dispatcher.InvokeAsync(() => TxtContactPerson.Focus());
                }
                return true;
            }
        }

        // Contact tab
        if (IsInside(focused, TxtContactPerson)) { TxtAddress.Focus(); return true; }
        if (IsInside(focused, TxtAddress)) { TxtComments.Focus(); return true; }

        // GST tab
        if (IsInside(focused, TxtGstNo)) { TxtPanNo.Focus(); return true; }
        if (IsInside(focused, TxtPanNo)) { CmbGstRegType.Focus(); return true; }
        if (IsInside(focused, CmbGstRegType)) { TxtCreditDays.Focus(); TxtCreditDays.SelectAll(); return true; }
        if (IsInside(focused, TxtCreditDays))
        {
            MainTabs.SelectedIndex = 3;
            Dispatcher.InvokeAsync(() => ChkMon.Focus());
            return true;
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

    private void ResetForm()
    {
        _editingId = 0;
        MainTabs.SelectedIndex = 0;

        TxtCode.Text = DatabaseService.GetNextDistributorCode();
        TxtName.Text = string.Empty;
        TxtShortName.Text = string.Empty;
        TxtLocation.Text = string.Empty;
        TxtLandmark.Text = string.Empty;
        TxtCity.Text = string.Empty;
        CmbState.Text = string.Empty;
        CmbCountry.Text = "India";
        TxtPinCode.Text = string.Empty;
        TxtTelOffice.Text = string.Empty;
        TxtPhone.Text = string.Empty;
        TxtEmail.Text = string.Empty;
        TxtFax.Text = string.Empty;
        CmbType.SelectedIndex = 0;
        CmbCurrency.Text = "INR";
        TxtTelRes.Text = string.Empty;
        TxtDistributorId.Text = string.Empty;
        TxtArea.Text = string.Empty;
        TxtBarcodeAlias.Text = string.Empty;

        TxtContactPerson.Text = string.Empty;
        TxtAddress.Text = string.Empty;
        TxtComments.Text = string.Empty;

        TxtGstNo.Text = string.Empty;
        TxtPanNo.Text = string.Empty;
        CmbGstRegType.SelectedIndex = 0;
        TxtCreditDays.Text = "0";

        ChkMon.IsChecked = ChkTue.IsChecked = ChkWed.IsChecked = ChkThu.IsChecked =
            ChkFri.IsChecked = ChkSat.IsChecked = ChkSun.IsChecked = false;

        ModeBanner.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4F46E5"));
        TxtModeBanner.Text = "(Mode - new) : Creating new record";
        BtnDelete.IsEnabled = false;
        TxtName.Focus();
    }

    private void LoadForEdit(DatabaseService.DistributorRecord d)
    {
        _editingId = d.Id;
        MainTabs.SelectedIndex = 0;

        TxtCode.Text = d.Code;
        TxtName.Text = d.Name;
        TxtShortName.Text = d.ShortName;
        TxtLocation.Text = d.Location;
        TxtLandmark.Text = d.Landmark;
        TxtCity.Text = d.City;
        CmbState.Text = d.State;
        CmbCountry.Text = string.IsNullOrWhiteSpace(d.Country) ? "India" : d.Country;
        TxtPinCode.Text = d.PinCode;
        TxtTelOffice.Text = d.TelOffice;
        TxtPhone.Text = d.Phone;
        TxtEmail.Text = d.Email;
        TxtFax.Text = d.Fax;
        SelectComboText(CmbType, d.DistType);
        CmbCurrency.Text = string.IsNullOrWhiteSpace(d.CurrencyName) ? "INR" : d.CurrencyName;
        TxtTelRes.Text = d.TelRes;
        TxtDistributorId.Text = d.DistributorId;
        TxtArea.Text = d.Area;
        TxtBarcodeAlias.Text = d.BarcodeAlias;

        TxtContactPerson.Text = d.ContactPerson;
        TxtAddress.Text = d.Address;
        TxtComments.Text = d.Comments;

        TxtGstNo.Text = d.GstNo;
        TxtPanNo.Text = d.PanNo;
        SelectComboText(CmbGstRegType, d.GstRegType);
        TxtCreditDays.Text = d.CreditDays.ToString();

        var days = (d.OrderDays ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        ChkMon.IsChecked = days.Contains("Mon", StringComparer.OrdinalIgnoreCase);
        ChkTue.IsChecked = days.Contains("Tue", StringComparer.OrdinalIgnoreCase);
        ChkWed.IsChecked = days.Contains("Wed", StringComparer.OrdinalIgnoreCase);
        ChkThu.IsChecked = days.Contains("Thu", StringComparer.OrdinalIgnoreCase);
        ChkFri.IsChecked = days.Contains("Fri", StringComparer.OrdinalIgnoreCase);
        ChkSat.IsChecked = days.Contains("Sat", StringComparer.OrdinalIgnoreCase);
        ChkSun.IsChecked = days.Contains("Sun", StringComparer.OrdinalIgnoreCase);

        ModeBanner.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB"));
        TxtModeBanner.Text = $"(Mode - edit) : Editing {d.Code} - {d.Name}";
        BtnDelete.IsEnabled = true;
        TxtName.Focus();
    }

    private static void SelectComboText(ComboBox cmb, string value)
    {
        foreach (ComboBoxItem item in cmb.Items)
        {
            if (string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                cmb.SelectedItem = item;
                return;
            }
        }
        cmb.SelectedIndex = 0;
    }

    private string BuildOrderDays()
    {
        var days = new List<string>();
        if (ChkMon.IsChecked == true) days.Add("Mon");
        if (ChkTue.IsChecked == true) days.Add("Tue");
        if (ChkWed.IsChecked == true) days.Add("Wed");
        if (ChkThu.IsChecked == true) days.Add("Thu");
        if (ChkFri.IsChecked == true) days.Add("Fri");
        if (ChkSat.IsChecked == true) days.Add("Sat");
        if (ChkSun.IsChecked == true) days.Add("Sun");
        return string.Join(",", days);
    }

    private string BuildDisplayAddress()
    {
        if (!string.IsNullOrWhiteSpace(TxtAddress.Text))
            return TxtAddress.Text.Trim();

        var parts = new[] { TxtLocation.Text, TxtLandmark.Text, TxtCity.Text, CmbState.Text, TxtPinCode.Text }
            .Select(s => s?.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s));
        return string.Join(", ", parts);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        string code = TxtCode.Text.Trim();
        string name = TxtName.Text.Trim();

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(name))
        {
            MessageBox.Show("Please enter required fields: * Code and * Name.", "Distributor Master",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            MainTabs.SelectedIndex = 0;
            if (string.IsNullOrEmpty(code)) TxtCode.Focus(); else TxtName.Focus();
            return;
        }

        int.TryParse(TxtCreditDays.Text.Trim(), out int creditDays);

        var record = new DatabaseService.DistributorRecord
        {
            Id = _editingId,
            Code = code,
            Name = name,
            ShortName = TxtShortName.Text,
            Location = TxtLocation.Text,
            Landmark = TxtLandmark.Text,
            City = TxtCity.Text,
            State = CmbState.Text,
            Country = CmbCountry.Text,
            PinCode = TxtPinCode.Text,
            TelOffice = TxtTelOffice.Text,
            Phone = TxtPhone.Text,
            Email = TxtEmail.Text,
            Fax = TxtFax.Text,
            DistType = (CmbType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "None",
            CurrencyName = CmbCurrency.Text,
            TelRes = TxtTelRes.Text,
            DistributorId = TxtDistributorId.Text,
            Area = TxtArea.Text,
            BarcodeAlias = TxtBarcodeAlias.Text,
            ContactPerson = TxtContactPerson.Text,
            Address = BuildDisplayAddress(),
            Comments = TxtComments.Text,
            GstNo = TxtGstNo.Text,
            PanNo = TxtPanNo.Text,
            GstRegType = (CmbGstRegType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Regular",
            CreditDays = creditDays,
            OrderDays = BuildOrderDays()
        };

        try
        {
            bool wasEdit = _editingId > 0;
            DatabaseService.SaveDistributor(record);
            ResetForm();
            UpdateCount();
            MessageBox.Show(wasEdit ? "Distributor updated successfully." : "Distributor created successfully.",
                "Distributor Master", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            MessageBox.Show($"Distributor code '{code}' already exists. Please use a unique code.",
                "Distributor Master", MessageBoxButton.OK, MessageBoxImage.Warning);
            MainTabs.SelectedIndex = 0;
            TxtCode.Focus();
        }
    }

    private void Help_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "Distributor Master Shortcuts:\n" +
            "• Enter : Move to next field\n" +
            "• F3 : New record\n" +
            "• F4 / F5 : View & Pick existing distributor to Edit\n" +
            "• F6 : Save distributor\n" +
            "• F7 : Clear form\n" +
            "• F10 : Delete (in Edit mode)\n" +
            "• F11 : Jump to Comments\n" +
            "• F12 : Close",
            "Distributor Master - Help", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void New_Click(object sender, RoutedEventArgs e) => ResetForm();
    private void Clear_Click(object sender, RoutedEventArgs e) => ResetForm();
    private void EditPick_Click(object sender, RoutedEventArgs e) => DistLookupOverlay.Show();
    private void Skip_Click(object sender, RoutedEventArgs e) => HandleEnterNavigation();

    private void Default_Click(object sender, RoutedEventArgs e)
    {
        CmbCountry.Text = "India";
        CmbCurrency.Text = "INR";
        CmbType.SelectedIndex = 1; // Local
        CmbGstRegType.SelectedIndex = 0; // Regular
        TxtCreditDays.Text = "0";
    }

    private void Comments_Click(object sender, RoutedEventArgs e)
    {
        MainTabs.SelectedIndex = 1;
        Dispatcher.InvokeAsync(() => TxtComments.Focus());
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_editingId <= 0) return;

        var res = MessageBox.Show($"Delete distributor '{TxtName.Text.Trim()}' ({TxtCode.Text.Trim()})?",
            "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (res != MessageBoxResult.Yes) return;

        DatabaseService.DeleteDistributor(_editingId);
        ResetForm();
        UpdateCount();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);

    private void NumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !e.Text.All(char.IsDigit);
    }
}
