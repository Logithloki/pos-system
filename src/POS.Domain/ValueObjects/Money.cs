using System.Globalization;

namespace POS.Domain.ValueObjects;

public readonly record struct Money
{
    public const int Scale = 2;

    public static readonly MidpointRounding RoundingMode = MidpointRounding.ToEven;

    public static readonly Money Zero = new(0m);

    public Money(decimal amount)
    {
        Amount = Round(amount);
    }

    public decimal Amount { get; }

    public static Money From(decimal amount)
    {
        return new Money(amount);
    }

    public static decimal Round(decimal amount)
    {
        return Math.Round(amount, Scale, RoundingMode);
    }

    public Money Abs()
    {
        return new Money(Math.Abs(Amount));
    }

    public bool IsNegative()
    {
        return Amount < 0;
    }

    public static Money operator +(Money left, Money right)
    {
        return new Money(left.Amount + right.Amount);
    }

    public static Money operator -(Money left, Money right)
    {
        return new Money(left.Amount - right.Amount);
    }

    public static Money operator *(Money value, decimal multiplier)
    {
        return new Money(value.Amount * multiplier);
    }

    public static Money operator /(Money value, decimal divisor)
    {
        if (divisor == 0)
        {
            throw new DivideByZeroException("Money division by zero is not allowed.");
        }

        return new Money(value.Amount / divisor);
    }

    public override string ToString()
    {
        return Amount.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
