using POS.Desktop.Infrastructure;

namespace POS.Desktop.Models;

public sealed class CartLineItem : BindableBase
{
    private int _quantity;

    public CartLineItem(long productId, string barcode, string name, decimal unitPrice, int quantity)
    {
        ProductId = productId;
        Barcode = barcode;
        Name = name;
        UnitPrice = unitPrice;
        _quantity = quantity;
    }

    public long ProductId { get; }

    public string Barcode { get; }

    public string Name { get; }

    public decimal UnitPrice { get; }

    public int Quantity
    {
        get => _quantity;
        set
        {
            if (SetProperty(ref _quantity, value))
            {
                RaisePropertyChanged(nameof(LineTotal));
            }
        }
    }

    public decimal LineTotal => Math.Round(UnitPrice * Quantity, 2, MidpointRounding.ToEven);
}
