using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using EmployeeMvc.Models;

namespace EmployeeMvc.Services
{
    public class EmailSender : IEmailSender
    {
        private static readonly Uri ResendEndpoint = new("https://api.resend.com/emails");
        private readonly EmailSettings _settings;
        private readonly ILogger<EmailSender> _logger;
        private readonly HttpClient _httpClient;

        public EmailSender(IOptions<EmailSettings> settings, ILogger<EmailSender> logger, HttpClient httpClient)
        {
            _settings = settings.Value;
            _logger = logger;
            _httpClient = httpClient;
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            if (string.IsNullOrWhiteSpace(_settings.ResendApiKey))
                throw new InvalidOperationException("EmailSettings:ResendApiKey is not configured.");

            if (string.IsNullOrWhiteSpace(_settings.SenderEmail))
                throw new InvalidOperationException("EmailSettings:SenderEmail is not configured.");

            using var request = new HttpRequestMessage(HttpMethod.Post, ResendEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ResendApiKey);
            request.Content = JsonContent.Create(new
            {
                from = $"{_settings.SenderName} <{_settings.SenderEmail}>",
                to = new[] { email },
                subject,
                html = htmlMessage
            });

            using var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Resend API rejected email to {Email}. Status: {StatusCode}; Response: {Response}",
                    email, (int)response.StatusCode, responseBody);
                throw new HttpRequestException($"Resend API returned {(int)response.StatusCode}: {responseBody}");
            }

            _logger.LogInformation("Email accepted by Resend for {Email}. Response: {Response}", email, responseBody);
        }
    }
}
