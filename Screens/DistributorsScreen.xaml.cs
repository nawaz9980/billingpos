using System;
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
    private string _editingCode = string.Empty;
    private readonly Control[] _detailsTabOrder;

    public DistributorsScreen()
    {
        InitializeComponent();

        _detailsTabOrder =
        [
            TxtName, TxtAddress, TxtPhone, TxtEmail, CmbType
        ];

        DistLookupOverlay.DistributorSelected += LoadForEdit;
        DistLookupOverlay.CloseRequested += () => TxtName.Focus();
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateCount();
        if (_editingId == 0)
            ResetForm();
        else
            TxtName.Focus();
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
                    Dispatcher.InvokeAsync(() => TxtGstNo.Focus());
                }
                return true;
            }
        }

        // GST tab order
        if (IsInside(focused, TxtGstNo)) { TxtPanNo.Focus(); return true; }
        if (IsInside(focused, TxtPanNo)) { CmbGstRegType.Focus(); return true; }
        if (IsInside(focused, CmbGstRegType)) { TxtCreditDays.Focus(); TxtCreditDays.SelectAll(); return true; }

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
        _editingCode = string.Empty;
        MainTabs.SelectedIndex = 0;

        TxtCode.Text = "(Auto-generated on Save)";
        TxtName.Text = string.Empty;
        TxtAddress.Text = string.Empty;
        TxtPhone.Text = string.Empty;
        TxtEmail.Text = string.Empty;
        CmbType.SelectedIndex = 0;
        TxtComments.Text = string.Empty;

        TxtGstNo.Text = string.Empty;
        TxtPanNo.Text = string.Empty;
        CmbGstRegType.SelectedIndex = 0;
        TxtCreditDays.Text = "0";

        ModeBanner.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4F46E5"));
        TxtModeBanner.Text = "(Mode - new) : Creating new record";
        BtnDelete.IsEnabled = false;
        TxtName.Focus();
    }

    private void LoadForEdit(DatabaseService.DistributorRecord d)
    {
        _editingId = d.Id;
        _editingCode = d.Code;
        MainTabs.SelectedIndex = 0;

        TxtCode.Text = d.Code;
        TxtName.Text = d.Name;
        TxtAddress.Text = d.Address;
        TxtPhone.Text = d.Phone;
        TxtEmail.Text = d.Email;
        SelectComboText(CmbType, d.DistType);
        TxtComments.Text = d.Comments;

        TxtGstNo.Text = d.GstNo;
        TxtPanNo.Text = d.PanNo;
        SelectComboText(CmbGstRegType, d.GstRegType);
        TxtCreditDays.Text = d.CreditDays.ToString();

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

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        string name = TxtName.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            MessageBox.Show("Please enter required field: * Name.", "Distributor Master",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            MainTabs.SelectedIndex = 0;
            TxtName.Focus();
            return;
        }

        bool isEdit = _editingId > 0;
        string code = isEdit ? _editingCode : DatabaseService.GetNextDistributorCode();
        int.TryParse(TxtCreditDays.Text.Trim(), out int creditDays);
        string distType = (CmbType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Local";
        string gstRegType = (CmbGstRegType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Regular";

        string summary =
            $"Code: {code}\n" +
            $"Name: {name}\n" +
            $"Address1: {(string.IsNullOrWhiteSpace(TxtAddress.Text) ? "-" : TxtAddress.Text.Trim())}\n" +
            $"Mobile: {(string.IsNullOrWhiteSpace(TxtPhone.Text) ? "-" : TxtPhone.Text.Trim())}\n" +
            $"Email: {(string.IsNullOrWhiteSpace(TxtEmail.Text) ? "-" : TxtEmail.Text.Trim())}\n" +
            $"Type: {distType}\n" +
            $"GSTIN: {(string.IsNullOrWhiteSpace(TxtGstNo.Text) ? "-" : TxtGstNo.Text.Trim())}\n" +
            $"PAN: {(string.IsNullOrWhiteSpace(TxtPanNo.Text) ? "-" : TxtPanNo.Text.Trim())}\n" +
            $"GST Reg. Type: {gstRegType}\n" +
            $"Credit Days: {creditDays}";

        var confirm = MessageBox.Show(
            $"{(isEdit ? "Confirm updating distributor:" : "Confirm saving new distributor:")}\n\n{summary}",
            "Confirm Distributor Details",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes)
            return;

        var record = new DatabaseService.DistributorRecord
        {
            Id = _editingId,
            Code = code,
            Name = name,
            Address = TxtAddress.Text,
            Phone = TxtPhone.Text,
            Email = TxtEmail.Text,
            DistType = distType,
            Comments = TxtComments.Text,
            GstNo = TxtGstNo.Text,
            PanNo = TxtPanNo.Text,
            GstRegType = gstRegType,
            CreditDays = creditDays
        };

        try
        {
            DatabaseService.SaveDistributor(record);
            ResetForm();
            UpdateCount();
            MessageBox.Show(
                isEdit
                    ? $"Distributor {code} ({name}) updated successfully."
                    : $"Distributor {code} ({name}) created successfully.",
                "Distributor Master",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            MessageBox.Show($"Distributor code '{code}' already exists.",
                "Distributor Master", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Help_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "Distributor Master Shortcuts:\n" +
            "• Enter : Move to next field\n" +
            "• F3 : New record\n" +
            "• F4 / F5 : View & Pick existing distributor to Edit\n" +
            "• F6 : Save distributor (auto-generates Code)\n" +
            "• F7 : Clear form\n" +
            "• F10 : Delete (in Edit mode)\n" +
            "• F11 : Jump to Comments\n" +
            "• F12 : Close",
            "Distributor Master - Help", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void New_Click(object sender, RoutedEventArgs e) => ResetForm();
    private void Clear_Click(object sender, RoutedEventArgs e) => ResetForm();
    private void EditPick_Click(object sender, RoutedEventArgs e) => DistLookupOverlay.Show();

    private void Comments_Click(object sender, RoutedEventArgs e)
    {
        MainTabs.SelectedIndex = 0;
        Dispatcher.InvokeAsync(() => TxtComments.Focus());
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_editingId <= 0) return;

        var res = MessageBox.Show($"Delete distributor '{TxtName.Text.Trim()}' ({_editingCode})?",
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
