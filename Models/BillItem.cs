using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace QuickPos.Models;

public class BillItem : INotifyPropertyChanged
{
    private int _qty = 1;

    public Product Product { get; set; } = new();

    public int Qty
    {
        get => _qty;
        set
        {
            if (_qty != value)
            {
                _qty = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Subtotal));
            }
        }
    }

    public decimal Subtotal => Product.Price * Qty;

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
