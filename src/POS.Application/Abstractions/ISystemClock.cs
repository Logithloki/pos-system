namespace POS.Application.Abstractions;

public interface ISystemClock
{
    DateTime UtcNow { get; }
}
