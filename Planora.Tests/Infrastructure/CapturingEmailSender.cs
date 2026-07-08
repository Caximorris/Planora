using System.Collections.Concurrent;
using Planora.Api.Application.Emails;
using Planora.Api.Application.Interfaces;

namespace Planora.Tests.Infrastructure;

/// <summary>
/// Test double for <see cref="IEmailSender"/> that records sent messages instead of
/// delivering them, so tests can assert an email was sent (and inspect its contents).
/// </summary>
public sealed class CapturingEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<SentEmail> _sent = new();

    public IReadOnlyCollection<SentEmail> Sent => _sent.ToArray();

    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        _sent.Enqueue(new SentEmail(
            message.To,
            message.Subject,
            message.HtmlBody,
            message.TextBody,
            message.From?.Address,
            message.From?.Format()));
        return Task.CompletedTask;
    }

    public record SentEmail(string To, string Subject, string Body, string? TextBody = null, string? FromAddress = null, string? From = null);
}
