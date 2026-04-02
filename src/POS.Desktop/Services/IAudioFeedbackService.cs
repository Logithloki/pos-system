namespace POS.Desktop.Services;

public interface IAudioFeedbackService
{
    void PlayScanSuccess();

    void PlayPaymentSuccess();

    void PlayError();
}
