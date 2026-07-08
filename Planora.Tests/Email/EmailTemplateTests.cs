using Planora.Api.Application.Emails;

namespace Planora.Tests.Email;

/// <summary>
/// Unit tests for the shared email template system (<see cref="PlanoraEmailFactory"/> +
/// <see cref="EmailLayout"/>). Pure rendering — no host, DB, or provider required. Verifies each
/// template renders, includes its required dynamic fields and CTA URL, ships a plain-text
/// alternative, and safely HTML-encodes untrusted input.
/// </summary>
public class EmailTemplateTests
{
    private static readonly EmailBranding Branding =
        new("Planora", "support@planora.website", "https://app.planora.website");

    [Fact]
    public void Verification_email_has_heading_cta_and_expiry()
    {
        var email = PlanoraEmailFactory.EmailVerification(Branding, "https://app.planora.website/confirm-email?token=abc");

        Assert.Equal("Verify your Planora email", email.Subject);
        Assert.Contains("Verify your email address", email.HtmlBody);
        Assert.Contains("https://app.planora.website/confirm-email?token=abc", email.HtmlBody);
        Assert.Contains("expires in 1 hour", email.HtmlBody);
        Assert.False(string.IsNullOrWhiteSpace(email.TextBody));
        Assert.Contains("https://app.planora.website/confirm-email?token=abc", email.TextBody);
    }

    [Fact]
    public void Password_reset_email_warns_if_not_requested()
    {
        var email = PlanoraEmailFactory.PasswordReset(Branding, "https://app.planora.website/reset-password?token=xyz");

        Assert.Equal("Reset your Planora password", email.Subject);
        Assert.Contains("Reset password", email.HtmlBody);
        Assert.Contains("/reset-password?token=xyz", email.HtmlBody);
        Assert.Contains("didn't request", email.HtmlBody);
    }

    [Fact]
    public void Security_alert_includes_timestamp_and_review_cta()
    {
        var when = new DateTimeOffset(2026, 7, 8, 14, 30, 0, TimeSpan.Zero);
        var email = PlanoraEmailFactory.PasswordChanged(Branding, when);

        Assert.Equal("Your Planora password was changed", email.Subject);
        Assert.Contains("2026", email.HtmlBody);
        Assert.Contains("UTC", email.HtmlBody);
        Assert.Contains("https://app.planora.website/profile", email.HtmlBody);
    }

    [Theory]
    [InlineData(true, "Two-factor authentication was enabled")]
    [InlineData(false, "Two-factor authentication was disabled")]
    public void Two_factor_alert_reflects_state(bool enabled, string expectedSubject)
    {
        var email = PlanoraEmailFactory.TwoFactorChanged(Branding, enabled, DateTimeOffset.UtcNow);
        Assert.Equal(expectedSubject, email.Subject);
    }

    [Fact]
    public void Invitation_email_names_inviter_workspace_and_link()
    {
        var email = PlanoraEmailFactory.WorkspaceInvitation(
            Branding, "Acme Team", "Dana", "https://app.planora.website/invite/tok123");

        Assert.Contains("Acme Team", email.HtmlBody);
        Assert.Contains("Dana", email.HtmlBody);
        Assert.Contains("Accept invitation", email.HtmlBody);
        Assert.Contains("/invite/tok123", email.HtmlBody);
    }

    [Fact]
    public void Notification_emails_carry_board_link()
    {
        var assigned = PlanoraEmailFactory.CardAssigned(Branding, "Ship v2", "https://app.planora.website/boards/1");
        Assert.Equal("You were assigned to a card", assigned.Subject);
        Assert.Contains("Ship v2", assigned.HtmlBody);
        Assert.Contains("/boards/1", assigned.HtmlBody);

        var comment = PlanoraEmailFactory.NewComment(Branding, "Ship v2", "Sam", "https://app.planora.website/boards/1");
        Assert.Equal("New comment on your card", comment.Subject);
        Assert.Contains("Sam", comment.HtmlBody);
    }

    [Fact]
    public void Every_email_is_a_full_html_document_with_a_plaintext_alternative()
    {
        var emails = new[]
        {
            PlanoraEmailFactory.EmailVerification(Branding, "https://x/y"),
            PlanoraEmailFactory.PasswordReset(Branding, "https://x/y"),
            PlanoraEmailFactory.PasswordChanged(Branding, DateTimeOffset.UtcNow),
            PlanoraEmailFactory.WorkspaceInvitation(Branding, "W", "I", "https://x/y"),
            PlanoraEmailFactory.CardAssigned(Branding, "C", "https://x/y"),
            PlanoraEmailFactory.NewComment(Branding, "C", "N", "https://x/y"),
        };

        foreach (var email in emails)
        {
            Assert.StartsWith("<!DOCTYPE html>", email.HtmlBody);
            Assert.Contains("</html>", email.HtmlBody);
            Assert.Contains("support@planora.website", email.HtmlBody);
            Assert.False(string.IsNullOrWhiteSpace(email.TextBody));
            Assert.DoesNotContain("<html", email.TextBody);
        }
    }

    [Fact]
    public void Untrusted_values_are_html_encoded()
    {
        var payload = "<script>alert('x')</script>";
        var email = PlanoraEmailFactory.CardAssigned(Branding, payload, "https://app.planora.website/boards/1");

        Assert.DoesNotContain("<script>", email.HtmlBody);
        Assert.Contains("&lt;script&gt;", email.HtmlBody);
        // The plain-text alternative keeps the raw value (no markup to inject into).
        Assert.Contains(payload, email.TextBody);
    }
}
