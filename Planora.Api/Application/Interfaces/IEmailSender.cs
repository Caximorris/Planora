using Planora.Api.Application.Emails;

namespace Planora.Api.Application.Interfaces;

/// <summary>
/// Abstracts outbound transactional email so the app is not coupled to a specific provider. A
/// console/dev sink is used locally; production swaps in a real provider behind this interface.
/// Callers pass a fully-composed <see cref="EmailMessage"/> (subject, HTML + optional plain text,
/// and an optional contextual From address).
/// </summary>
public interface IEmailSender
{
    /// <summary>Sends a composed email to a single recipient.</summary>
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}
