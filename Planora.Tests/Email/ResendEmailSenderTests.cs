using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Planora.Api.Application.Options;
using Planora.Api.Infrastructure.Email;

namespace Planora.Tests.Email;

public class ResendEmailSenderTests
{
    [Fact]
    public async Task SendAsync_posts_expected_resend_request()
    {
        var handler = new CapturingHandler(HttpStatusCode.OK, "{\"id\":\"email_123\"}");
        var sender = new ResendEmailSender(new HttpClient(handler), Options.Create(new EmailOptions
        {
            From = new EmailFromOptions
            {
                Address = "notifications@planora.website",
                Name = "Planora"
            },
            Resend = new ResendOptions { ApiKey = "re_test_key" }
        }));

        await sender.SendAsync("matias@example.com", "Subject", "<p>Hello</p>");

        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("https://api.resend.com/emails", handler.RequestUri?.ToString());
        Assert.Equal("Bearer", handler.Authorization?.Scheme);
        Assert.Equal("re_test_key", handler.Authorization?.Parameter);
        Assert.Contains("Planora.Api", handler.UserAgent);
        Assert.Equal("application/json", handler.Accept);

        using var json = JsonDocument.Parse(handler.Body!);
        var root = json.RootElement;
        Assert.Equal("Planora <notifications@planora.website>", root.GetProperty("from").GetString());
        Assert.Equal("matias@example.com", root.GetProperty("to")[0].GetString());
        Assert.Equal("Subject", root.GetProperty("subject").GetString());
        Assert.Equal("<p>Hello</p>", root.GetProperty("html").GetString());
    }

    [Fact]
    public async Task SendAsync_throws_with_resend_error_body()
    {
        var handler = new CapturingHandler(HttpStatusCode.UnprocessableEntity, "{\"message\":\"Invalid from\"}");
        var sender = new ResendEmailSender(new HttpClient(handler), Options.Create(new EmailOptions
        {
            From = new EmailFromOptions { Address = "notifications@planora.website" },
            Resend = new ResendOptions { ApiKey = "re_test_key" }
        }));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sender.SendAsync("matias@example.com", "Subject", "<p>Hello</p>"));

        Assert.Contains("422", ex.Message);
        Assert.Contains("Invalid from", ex.Message);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseBody;

        public CapturingHandler(HttpStatusCode statusCode, string responseBody)
        {
            _statusCode = statusCode;
            _responseBody = responseBody;
        }

        public HttpMethod? Method { get; private set; }
        public Uri? RequestUri { get; private set; }
        public AuthenticationHeaderValue? Authorization { get; private set; }
        public string UserAgent { get; private set; } = "";
        public string? Accept { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri;
            Authorization = request.Headers.Authorization;
            UserAgent = string.Join(" ", request.Headers.UserAgent.Select(v => v.ToString()));
            Accept = request.Headers.Accept.FirstOrDefault()?.MediaType;
            Body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody)
            };
        }
    }
}
