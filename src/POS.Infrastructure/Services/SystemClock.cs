using POS.Application.Abstractions;

namespace POS.Infrastructure.Services;

public sealed class SystemClock : ISystemClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
