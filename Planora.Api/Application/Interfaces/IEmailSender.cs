namespace Planora.Api.Application.Interfaces;

/// <summary>
/// Abstracts outbound transactional email (password reset, email verification,
/// later notifications) so the app is not coupled to a specific provider. A
/// console/dev sink is used locally; production swaps in a real provider behind
/// this interface.
/// </summary>
public interface IEmailSender
{
    /// <summary>Sends an HTML email to a single recipient.</summary>
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default);
}
