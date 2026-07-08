using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Planora.Api.Application.Emails;
using Planora.Api.Application.Interfaces;
using Planora.Api.Application.Options;

namespace Planora.Api.Infrastructure.Email;

/// <summary>
/// Production <see cref="IEmailSender"/> backed by the Resend HTTP API. Throws on a non-success
/// response so callers (which wrap sends in try/catch) log the failure without breaking the user
/// action. The API key is attached per-request from configuration and is never logged.
/// </summary>
public sealed class ResendEmailSender : IEmailSender
{
    private const string SendUrl = "https://api.resend.com/emails";
    private const string UserAgent = "Planora.Api/1.0";

    private readonly HttpClient _http;
    private readonly EmailOptions _options;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ResendEmailSender(HttpClient http, IOptions<EmailOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        // Prefer the message's contextual sender; fall back to the default configured From identity.
        var from = message.From?.Format()
            ?? (string.IsNullOrWhiteSpace(_options.From.Name)
                ? _options.From.Address
                : $"{_options.From.Name} <{_options.From.Address}>");

        using var request = new HttpRequestMessage(HttpMethod.Post, SendUrl)
        {
            Content = JsonContent.Create(new
            {
                from,
                to = new[] { message.To },
                subject = message.Subject,
                html = message.HtmlBody,
                text = message.TextBody
            }, options: JsonOptions)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.Resend.ApiKey);
        request.Headers.UserAgent.ParseAdd(UserAgent);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _http.SendAsync(request, ct);
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(ct);
        throw new InvalidOperationException(
            $"Resend email send failed with {(int)response.StatusCode} {response.ReasonPhrase}: {body}");
    }
}
