using POS.Domain.Enums;

namespace POS.Desktop.Services;

public sealed class OperatorSessionContext : IOperatorSessionContext
{
    private readonly object _syncRoot = new();

    private bool _isInitialized;
    private long _operatorUserId;
    private UserRole _operatorRole = UserRole.Cashier;

    public bool IsInitialized
    {
        get
        {
            lock (_syncRoot)
            {
                return _isInitialized;
            }
        }
    }

    public long OperatorUserId
    {
        get
        {
            lock (_syncRoot)
            {
                return _operatorUserId;
            }
        }
    }

    public UserRole OperatorRole
    {
        get
        {
            lock (_syncRoot)
            {
                return _operatorRole;
            }
        }
    }

    public void SetOperator(long userId, UserRole role)
    {
        if (userId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(userId), "Operator user id must be greater than zero.");
        }

        lock (_syncRoot)
        {
            _operatorUserId = userId;
            _operatorRole = role;
            _isInitialized = true;
        }
    }
}
