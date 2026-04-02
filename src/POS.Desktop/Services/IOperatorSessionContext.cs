using POS.Domain.Enums;

namespace POS.Desktop.Services;

public interface IOperatorSessionContext
{
    bool IsInitialized { get; }

    long OperatorUserId { get; }

    UserRole OperatorRole { get; }

    void SetOperator(long userId, UserRole role);
}
