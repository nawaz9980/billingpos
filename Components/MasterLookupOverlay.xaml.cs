using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QuickPos.Services;

namespace QuickPos.Components;

public sealed class MasterLookupRow : INotifyPropertyChanged
{
    private bool _isSelected;
    public int Sno { get; set; }
    public string Name { get; set; } = string.Empty;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public partial class MasterLookupOverlay : UserControl
{
    public event Action<string, List<string>>? ItemsSelected; // (tableName, selectedValues)
    public event Action<string>? CloseRequested;              // (tableName)

    private string _tableName = "Brands";
    private bool _multiSelect;
    private List<MasterLookupRow> _all = [];
    private List<MasterLookupRow> _filtered = [];

    public MasterLookupOverlay()
    {
        InitializeComponent();
    }

    public void Show(string tableName, string title, bool multiSelect, IEnumerable<string>? preselected = null, string initialFilter = "")
    {
        _tableName = tableName;
        _multiSelect = multiSelect;
        TxtTitle.Text = title;

        ColCheck.Visibility = multiSelect ? Visibility.Visible : Visibility.Collapsed;
        BtnDone.Visibility = multiSelect ? Visibility.Visible : Visibility.Collapsed;
        TxtSearch.CharacterCasing = string.Equals(tableName, "Sizes", StringComparison.OrdinalIgnoreCase)
            ? CharacterCasing.Upper
            : CharacterCasing.Normal;
        TxtHint.Text = multiSelect
            ? "Tick multiple • Enter Apply • Esc Close"
            : "↑/↓ Select • Enter/Double-Click Pick • Esc Close";

        Visibility = Visibility.Visible;
        LoadItems(preselected);
        TxtSearch.Text = initialFilter;
        ApplyFilter(initialFilter);

        Dispatcher.InvokeAsync(() =>
        {
            TxtSearch.Focus();
            TxtSearch.CaretIndex = TxtSearch.Text.Length;
        }, System.Windows.Threading.DispatcherPriority.Input);
    }

    public void Hide()
    {
        Visibility = Visibility.Collapsed;
    }

    private void LoadItems(IEnumerable<string>? preselected)
    {
        var preSet = new HashSet<string>(preselected ?? [], StringComparer.OrdinalIgnoreCase);
        var names = DatabaseService.GetMasterNames(_tableName);
        _all = names.Select((n, idx) => new MasterLookupRow
        {
            Sno = idx + 1,
            Name = n,
            IsSelected = preSet.Contains(n)
        }).ToList();
    }

    private void ApplyFilter(string query)
    {
        string q = query.Trim().ToLowerInvariant();
        _filtered = string.IsNullOrEmpty(q)
            ? [.. _all]
            : _all.Where(x => x.Name.ToLowerInvariant().Contains(q)).ToList();

        GridItems.ItemsSource = _filtered;
        if (_filtered.Count > 0)
        {
            GridItems.SelectedIndex = 0;
            GridItems.ScrollIntoView(_filtered[0]);
        }
        UpdateCount();
    }

    private void UpdateCount()
    {
        if (_multiSelect)
        {
            int checkedCount = _all.Count(x => x.IsSelected);
            TxtCount.Text = $"{checkedCount} selected / {_all.Count}";
        }
        else
        {
            int total = _filtered.Count;
            int cur = GridItems.SelectedIndex >= 0 ? GridItems.SelectedIndex + 1 : 0;
            TxtCount.Text = $"{cur}/{total}";
        }
    }

    private void CommitSelection()
    {
        if (_multiSelect)
        {
            var selected = _all.Where(x => x.IsSelected).Select(x => x.Name).ToList();
            Hide();
            ItemsSelected?.Invoke(_tableName, selected);
        }
        else if (GridItems.SelectedItem is MasterLookupRow row)
        {
            Hide();
            ItemsSelected?.Invoke(_tableName, [row.Name]);
        }
    }

    private void AddNew_Click(object sender, RoutedEventArgs e)
    {
        string name = TxtSearch.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            TxtSearch.Focus();
            return;
        }

        if (string.Equals(_tableName, "Sizes", StringComparison.OrdinalIgnoreCase))
            name = name.ToUpperInvariant();

        string label = _tableName switch
        {
            "Sizes" => "Size",
            "Brands" => "Brand",
            "Styles" => "Style",
            _ => "Item"
        };

        var confirm = MessageBox.Show(
            $"Add new {label} '{name}'?",
            $"Confirm New {label}",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes)
        {
            TxtSearch.Focus();
            return;
        }

        DatabaseService.AddMasterName(_tableName, name);

        if (_multiSelect)
        {
            var existing = _all.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.IsSelected = true;
            }
            else
            {
                _all.Add(new MasterLookupRow { Sno = _all.Count + 1, Name = name, IsSelected = true });
            }
            TxtSearch.Clear();
            ApplyFilter(string.Empty);
            TxtSearch.Focus();
        }
        else
        {
            Hide();
            ItemsSelected?.Invoke(_tableName, [name]);
        }
    }

    private void RowCheck_Click(object sender, RoutedEventArgs e) => UpdateCount();
    private void Done_Click(object sender, RoutedEventArgs e) => CommitSelection();

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter(TxtSearch.Text);

    private void TxtSearch_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down && _filtered.Count > 0)
        {
            int next = Math.Min(_filtered.Count - 1, GridItems.SelectedIndex + 1);
            GridItems.SelectedIndex = next;
            GridItems.ScrollIntoView(_filtered[next]);
            e.Handled = true;
        }
        else if (e.Key == Key.Up && _filtered.Count > 0)
        {
            int prev = Math.Max(0, GridItems.SelectedIndex - 1);
            GridItems.SelectedIndex = prev;
            GridItems.ScrollIntoView(_filtered[prev]);
            e.Handled = true;
        }
        else if (e.Key == Key.Space && _multiSelect && string.IsNullOrEmpty(TxtSearch.Text) && GridItems.SelectedItem is MasterLookupRow row)
        {
            row.IsSelected = !row.IsSelected;
            UpdateCount();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (_multiSelect)
            {
                if (!string.IsNullOrWhiteSpace(TxtSearch.Text) && _filtered.Count == 0)
                    AddNew_Click(this, new RoutedEventArgs());
                else
                    CommitSelection();
            }
            else
            {
                if (GridItems.SelectedItem is MasterLookupRow)
                    CommitSelection();
                else if (!string.IsNullOrWhiteSpace(TxtSearch.Text))
                    AddNew_Click(this, new RoutedEventArgs());
            }
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Hide();
            CloseRequested?.Invoke(_tableName);
        }
    }

    private void GridItems_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space && _multiSelect && GridItems.SelectedItem is MasterLookupRow row)
        {
            row.IsSelected = !row.IsSelected;
            UpdateCount();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            CommitSelection();
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Hide();
            CloseRequested?.Invoke(_tableName);
        }
    }

    private void GridItems_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_multiSelect && GridItems.SelectedItem is MasterLookupRow row)
        {
            row.IsSelected = !row.IsSelected;
            UpdateCount();
        }
        else
        {
            CommitSelection();
        }
    }

    private void GridItems_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateCount();

    private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Hide();
            CloseRequested?.Invoke(_tableName);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        CloseRequested?.Invoke(_tableName);
    }
}
