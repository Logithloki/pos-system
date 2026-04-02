namespace POS.Desktop.Models;

public sealed record CatalogProduct(long Id, string Barcode, string Name, decimal Price, decimal Cost);
