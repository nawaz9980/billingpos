using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QuickPos.Screens;

namespace QuickPos;

public partial class MainWindow : Window
{
    private readonly DashboardScreen _dashboardScreen;
    private readonly SalesBillScreen _salesBillScreen;
    private readonly SalesReturnScreen _salesReturnScreen;
    private readonly ProductsScreen _productsScreen;
    private readonly InventoryScreen _inventoryScreen;
    private readonly PurchaseInvoiceScreen _purchaseInvoiceScreen;
    private readonly DistributorsScreen _distributorsScreen;

    // Track only screens that have been opened by the user
    private readonly List<UserControl> _openedScreens = [];

    // Helper representation for a navigation stop (screen + specific bill index if SalesBill)
    private readonly record struct NavStop(UserControl Screen, int BillIndex = -1);

    public MainWindow()
    {
        InitializeComponent();

        _dashboardScreen = new DashboardScreen();
        _salesBillScreen = new SalesBillScreen();
        _salesReturnScreen = new SalesReturnScreen();
        _productsScreen = new ProductsScreen();
        _inventoryScreen = new InventoryScreen();
        _purchaseInvoiceScreen = new PurchaseInvoiceScreen();
        _distributorsScreen = new DistributorsScreen();

        // Dashboard is the default home screen and always part of opened screens
        _openedScreens.Add(_dashboardScreen);

        // Wire Dashboard right-menu clicks to switch screens
        _dashboardScreen.ScreenRequested += (s, key) => TopNav_MenuItemSelected(s, key);

        // Wire Sales Bill F12 / Close button: remove from opened screens and return to Dashboard
        _salesBillScreen.CloseRequested += (s, e) =>
        {
            _openedScreens.Remove(_salesBillScreen);
            NavigateTo(_dashboardScreen);
        };

        // Wire Sales Bill F3 / Sales Return button
        _salesBillScreen.ReturnRequested += (s, e) => NavigateTo(_salesReturnScreen);

        // Wire Sales Return F12 / Close button
        _salesReturnScreen.CloseRequested += (s, e) =>
        {
            _openedScreens.Remove(_salesReturnScreen);
            NavigateTo(_dashboardScreen);
        };

        // Wire Purchase Invoice F12 / Close button
        _purchaseInvoiceScreen.CloseRequested += (s, e) =>
        {
            _openedScreens.Remove(_purchaseInvoiceScreen);
            NavigateTo(_dashboardScreen);
        };

        // Wire Distributors Master F12 / Close button (closes modal overlay back to underlying screen)
        _distributorsScreen.CloseRequested += (s, e) =>
        {
            ModalOverlayFrame.Visibility = Visibility.Collapsed;
            ModalOverlayFrame.Content = null;
        };

        // Wire TopNav Events
        TopNav.HomeClicked += (s, e) => NavigateTo(_dashboardScreen);
        TopNav.MenuItemSelected += TopNav_MenuItemSelected;

        TopNav.HamburgerClicked += (s, e) => NavigateTo(_dashboardScreen);
        TopNav.NotificationClicked += (s, e) => MessageBox.Show("3 unread store alerts:\n- 2 online orders awaiting pickup\n- 1 low stock warning", "Notifications", MessageBoxButton.OK, MessageBoxImage.Information);
        TopNav.SearchClicked += (s, e) =>
        {
            NavigateTo(_salesBillScreen);
            _salesBillScreen.TxtBarcodeEntry.Focus();
        };
        TopNav.AvatarClicked += (s, e) => MessageBox.Show("User: Nawaz (Admin / Manager)\nTerminal: #1 (Main Register)", "User Profile", MessageBoxButton.OK, MessageBoxImage.Information);

        // Start on Dashboard (HomePage)
        NavigateTo(_dashboardScreen);
    }

    public void NavigateTo(UserControl screen)
    {
        if (!_openedScreens.Contains(screen))
        {
            _openedScreens.Add(screen);
        }
        MainContentFrame.Content = screen;
    }

    // ════════════════════ GLOBAL CTRL + TAB NAVIGATION ════════════════════

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Ctrl + Tab (and Ctrl + Shift + Tab) cycles ONLY across user-opened screens and all held bills till HomePage
        if (e.Key == Key.Tab && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            e.Handled = true;
            bool isBackward = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            CycleScreen(isBackward);
        }
    }

    private List<NavStop> BuildNavStops()
    {
        var stops = new List<NavStop>();

        // 1. Dashboard (HomePage) is always present
        stops.Add(new NavStop(_dashboardScreen));

        // 2. Add each user-opened screen
        foreach (var screen in _openedScreens)
        {
            if (screen == _dashboardScreen)
                continue;

            if (screen == _salesBillScreen)
            {
                int billCount = _salesBillScreen.OpenBillsCount;
                if (billCount <= 1)
                {
                    stops.Add(new NavStop(_salesBillScreen, 0));
                }
                else
                {
                    // Include every active and held bill as an individual stop
                    for (int i = 0; i < billCount; i++)
                    {
                        stops.Add(new NavStop(_salesBillScreen, i));
                    }
                }
            }
            else
            {
                stops.Add(new NavStop(screen));
            }
        }

        return stops;
    }

    private void CycleScreen(bool isBackward)
    {
        var stops = BuildNavStops();
        if (stops.Count <= 1) return;

        var current = MainContentFrame.Content as UserControl ?? _dashboardScreen;

        // Locate current stop index in the dynamic stops list
        int currentIndex;
        if (current == _salesBillScreen)
        {
            int currentBillIdx = _salesBillScreen.ActiveBillIndex;
            currentIndex = stops.FindIndex(s => s.Screen == _salesBillScreen && s.BillIndex == currentBillIdx);
        }
        else
        {
            currentIndex = stops.FindIndex(s => s.Screen == current);
        }

        if (currentIndex < 0) currentIndex = 0;

        // Calculate next stop index (forward or backward)
        int nextIndex = isBackward
            ? (currentIndex - 1 + stops.Count) % stops.Count
            : (currentIndex + 1) % stops.Count;

        var target = stops[nextIndex];

        if (target.Screen == _salesBillScreen)
        {
            if (MainContentFrame.Content != _salesBillScreen)
            {
                MainContentFrame.Content = _salesBillScreen;
            }
            _salesBillScreen.ActivateBillByIndex(target.BillIndex);
        }
        else
        {
            MainContentFrame.Content = target.Screen;
        }
    }

    private void TopNav_MenuItemSelected(object? sender, string screenKey)
    {
        switch (screenKey.ToLowerInvariant())
        {
            case "salesbill":
            case "orders":
            case "heldbills":
                NavigateTo(_salesBillScreen);
                break;

            case "refunds":
                NavigateTo(_salesReturnScreen);
                break;

            case "purchaseinvoice":
                NavigateTo(_purchaseInvoiceScreen);
                break;

            case "purchasereturn":
                MessageBox.Show("Purchase Return module coming soon!", "QuickPOS - Purchase", MessageBoxButton.OK, MessageBoxImage.Information);
                break;

            case "distributors":
                ModalOverlayFrame.Content = _distributorsScreen;
                ModalOverlayFrame.Visibility = Visibility.Visible;
                break;

            case "products":
                NavigateTo(_productsScreen);
                break;

            case "inventory":
                NavigateTo(_inventoryScreen);
                break;

            case "customers":
                MessageBox.Show("Customers directory opened", "QuickPOS");
                break;

            case "reports":
                MessageBox.Show("Sales and register shift reports opened", "QuickPOS");
                break;

            case "settings":
                MessageBox.Show("Thermal printer & store settings opened", "QuickPOS");
                break;

            default:
                NavigateTo(_dashboardScreen);
                break;
        }
    }
}
