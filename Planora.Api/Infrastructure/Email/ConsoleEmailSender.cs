using Planora.Api.Application.Emails;
using Planora.Api.Application.Interfaces;

namespace Planora.Api.Infrastructure.Email;

/// <summary>
/// Development email sink: logs the message instead of sending it, so flows like
/// password reset are exercisable locally without a paid email provider. The body
/// (which in dev may contain a reset link/token) is intentionally logged here —
/// this implementation must NOT be registered in production; swap in a real
/// provider behind <see cref="IEmailSender"/> for deployed environments.
/// </summary>
public sealed class ConsoleEmailSender : IEmailSender
{
    private readonly ILogger<ConsoleEmailSender> _logger;

    public ConsoleEmailSender(ILogger<ConsoleEmailSender> logger) => _logger = logger;

    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "EMAIL_DEV_SINK From={From} To={To} Subject={Subject}\n{Body}",
            message.From?.Format() ?? "(default)", message.To, message.Subject, message.HtmlBody);
        return Task.CompletedTask;
    }
}
