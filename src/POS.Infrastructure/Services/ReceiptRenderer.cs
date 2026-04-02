using System.Text;
using POS.Domain.Entities;

namespace POS.Infrastructure.Services;

public static class ReceiptRenderer
{
    public sealed record RenderLine(string Name, int Quantity, decimal UnitPrice, decimal LineTotal);

    private const int ThermalLineWidth = 40;

    public static string Render(string storeName, SalesOrder salesOrder, IReadOnlyCollection<RenderLine> lines)
    {
        var builder = new StringBuilder();
        var divider = new string('-', ThermalLineWidth);

        builder.AppendLine(Center(storeName, ThermalLineWidth));
        builder.AppendLine(Center(salesOrder.OrderType == POS.Domain.Enums.SalesOrderType.Reversal ? "REFUND REVERSAL" : "SALES RECEIPT", ThermalLineWidth));
        builder.AppendLine(divider);
        builder.AppendLine($"Receipt: {salesOrder.ReceiptNumber}");
        builder.AppendLine($"Date: {salesOrder.CreatedUtc:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine($"Cashier: {salesOrder.UserId}");
        builder.AppendLine(divider);

        foreach (var line in lines)
        {
            builder.AppendLine(Truncate(line.Name, ThermalLineWidth));
            var detail = $"{line.Quantity,3} x {line.UnitPrice,8:0.00} = {line.LineTotal,10:0.00}";
            builder.AppendLine(PadRight(detail, ThermalLineWidth));
        }

        builder.AppendLine(divider);
        builder.AppendLine($"Subtotal:      {salesOrder.TotalBeforeDiscount,12:0.00}");
        builder.AppendLine($"Discount:      {salesOrder.DiscountAmount,12:0.00}");
        builder.AppendLine($"Tax:           {salesOrder.TaxAmount,12:0.00}");
        builder.AppendLine($"TOTAL:         {salesOrder.TotalAfterTax,12:0.00}");
        builder.AppendLine($"Payment:       {salesOrder.PaymentMethod}");
        builder.AppendLine($"Tendered:      {salesOrder.AmountTendered,12:0.00}");
        builder.AppendLine(divider);
        builder.AppendLine(Center("Thank you", ThermalLineWidth));

        return builder.ToString();
    }

    private static string Center(string text, int width)
    {
        if (text.Length >= width)
        {
            return text;
        }

        var leftPadding = (width - text.Length) / 2;
        return new string(' ', leftPadding) + text;
    }

    private static string PadRight(string text, int width)
    {
        if (text.Length >= width)
        {
            return text;
        }

        return text.PadRight(width);
    }

    private static string Truncate(string text, int width)
    {
        if (text.Length <= width)
        {
            return text;
        }

        return text[..width];
    }
}
