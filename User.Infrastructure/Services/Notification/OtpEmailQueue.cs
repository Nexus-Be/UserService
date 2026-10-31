namespace User.Infrastructure.Services.Notification;

using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

public sealed record OtpEmailMessage(
    string Email,
    string FullName,
    string OtpCode,
    int ExpiresInMinutes);

public sealed class OtpEmailQueue
{
    private const int Capacity = 1000;
    private readonly Channel<OtpEmailMessage> _channel = Channel.CreateBounded<OtpEmailMessage>(
        new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });

    public bool TryEnqueue(OtpEmailMessage message) => _channel.Writer.TryWrite(message);

    public ValueTask<OtpEmailMessage> DequeueAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAsync(cancellationToken);
}
