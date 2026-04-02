using POS.Desktop.Infrastructure;

namespace POS.Desktop.ViewModels;

public abstract class ManagementSectionViewModelBase : BindableBase
{
    private string _statusMessage = "Ready.";
    private bool _isError;

    public string StatusMessage
    {
        get => _statusMessage;
        protected set => SetProperty(ref _statusMessage, value);
    }

    public bool IsError
    {
        get => _isError;
        protected set => SetProperty(ref _isError, value);
    }

    protected void SetInfo(string message)
    {
        StatusMessage = message;
        IsError = false;
    }

    protected void SetError(string message)
    {
        StatusMessage = message;
        IsError = true;
    }
}
