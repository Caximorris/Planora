using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Planora.Api.Application.Emails;
using Planora.Api.Application.Options;

namespace Planora.Tests.Email;

/// <summary>
/// Unit tests for <see cref="EmailSenderResolver"/> — the central mapping of email category to a
/// From address on the verified domain, including environment-variable overrides.
/// </summary>
public class EmailSenderResolverTests
{
    private static EmailSenderResolver Build(EmailOptions options, IDictionary<string, string?>? env = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(env ?? new Dictionary<string, string?>())
            .Build();
        return new EmailSenderResolver(Options.Create(options), config);
    }

    private static EmailOptions DefaultOptions() => new()
    {
        Domain = "planora.website",
        From = new EmailFromOptions { Address = "notifications@planora.website", Name = "Planora" }
    };

    [Theory]
    [InlineData(EmailSenderKind.NoReply, "no-reply@planora.website")]
    [InlineData(EmailSenderKind.Security, "security@planora.website")]
    [InlineData(EmailSenderKind.Invites, "invites@planora.website")]
    [InlineData(EmailSenderKind.Notifications, "notifications@planora.website")]
    [InlineData(EmailSenderKind.Support, "support@planora.website")]
    public void Resolves_local_part_on_verified_domain(EmailSenderKind kind, string expected)
    {
        var resolver = Build(DefaultOptions());
        Assert.Equal(expected, resolver.Resolve(kind).Address);
    }

    [Fact]
    public void Security_sender_has_distinct_display_name()
    {
        var resolver = Build(DefaultOptions());
        var security = resolver.Resolve(EmailSenderKind.Security);
        Assert.Equal("Planora Security", security.Name);
        Assert.Equal("Planora Security <security@planora.website>", security.Format());
    }

    [Fact]
    public void Environment_variable_overrides_address()
    {
        var resolver = Build(DefaultOptions(), new Dictionary<string, string?>
        {
            ["EMAIL_FROM_SECURITY"] = "alerts@planora.website"
        });
        Assert.Equal("alerts@planora.website", resolver.Resolve(EmailSenderKind.Security).Address);
    }

    [Fact]
    public void EMAIL_DOMAIN_env_drives_composition()
    {
        var options = new EmailOptions
        {
            From = new EmailFromOptions { Address = "notifications@planora.website", Name = "Planora" }
        };
        var resolver = Build(options, new Dictionary<string, string?> { ["EMAIL_DOMAIN"] = "mail.planora.website" });
        Assert.Equal("no-reply@mail.planora.website", resolver.Resolve(EmailSenderKind.NoReply).Address);
    }

    [Fact]
    public void Domain_falls_back_to_default_from_host()
    {
        var options = new EmailOptions
        {
            From = new EmailFromOptions { Address = "notifications@planora.website", Name = "Planora" }
        };
        var resolver = Build(options);
        Assert.Equal("planora.website", resolver.Domain);
        Assert.Equal("invites@planora.website", resolver.Resolve(EmailSenderKind.Invites).Address);
    }
}
