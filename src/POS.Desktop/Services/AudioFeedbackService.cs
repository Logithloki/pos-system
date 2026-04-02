using System.Media;

namespace POS.Desktop.Services;

public sealed class AudioFeedbackService : IAudioFeedbackService
{
    public void PlayScanSuccess()
    {
        SystemSounds.Asterisk.Play();
    }

    public void PlayPaymentSuccess()
    {
        SystemSounds.Exclamation.Play();
    }

    public void PlayError()
    {
        SystemSounds.Hand.Play();
    }
}
