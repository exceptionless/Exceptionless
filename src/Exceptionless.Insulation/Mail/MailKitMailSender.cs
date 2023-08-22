﻿using System.Diagnostics;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Exceptionless.DateTimeExtensions;
using Foundatio.Utility;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using MimeKit;
using MailMessage = Exceptionless.Core.Queues.Models.MailMessage;

namespace Exceptionless.Insulation.Mail;

public class MailKitMailSender : IMailSender, IHealthCheck
{
    private readonly EmailOptions _emailOptions;
    private readonly ILogger _logger;
    private DateTime _lastSuccessfulConnection = DateTime.MinValue;

    public MailKitMailSender(EmailOptions emailOptions, ILoggerFactory loggerFactory)
    {
        _emailOptions = emailOptions;
        _logger = loggerFactory.CreateLogger<MailKitMailSender>();
    }

    public async Task SendAsync(MailMessage model)
    {
        string? host = _emailOptions.SmtpHost;
        if (String.IsNullOrEmpty(host))
            throw new Exception("Email Host is not configured");

        _logger.LogTrace("Creating Mail Message from model");

        var message = CreateMailMessage(model);
        message.Headers.Add("X-Mailer-Machine", Environment.MachineName);
        message.Headers.Add("X-Mailer-Date", SystemClock.UtcNow.ToString());
        message.Headers.Add("X-Auto-Response-Suppress", "All");
        message.Headers.Add("Auto-Submitted", "auto-generated");

        using var client = new SmtpClient(new ExtensionsProtocolLogger(_logger));
        int port = _emailOptions.SmtpPort;
        var encryption = GetSecureSocketOption(_emailOptions.SmtpEncryption);
        _logger.LogTrace("Connecting to SMTP server: {SmtpHost}:{SmtpPort} using {Encryption}", host, port, encryption);

        var sw = Stopwatch.StartNew();
        await client.ConnectAsync(host, port, encryption).AnyContext();
        _logger.LogTrace("Connected to SMTP server took {Duration:g}", sw.Elapsed);

        // Note: since we don't have an OAuth2 token, disable the XOAUTH2 authentication mechanism.
        client.AuthenticationMechanisms.Remove("XOAUTH2");

        string? user = _emailOptions.SmtpUser;
        if (!String.IsNullOrEmpty(user))
        {
            _logger.LogTrace("Authenticating {SmtpUser} to SMTP server", user);
            sw.Restart();
            await client.AuthenticateAsync(user, _emailOptions.SmtpPassword).AnyContext();
            _logger.LogTrace("Authenticated to SMTP server took {Duration:g}", user, sw.Elapsed);
        }

        _logger.LogTrace("Sending message: to={To} subject={Subject}", message.Subject, message.To);
        sw.Restart();
        await client.SendAsync(message).AnyContext();
        _logger.LogTrace("Sent Message took {Duration:g}", sw.Elapsed);

        sw.Restart();
        await client.DisconnectAsync(true).AnyContext();
        _logger.LogTrace("Disconnected from SMTP server took {Duration:g}", sw.Elapsed);
        sw.Stop();

        _lastSuccessfulConnection = SystemClock.UtcNow;
    }

    private SecureSocketOptions GetSecureSocketOption(SmtpEncryption encryption)
    {
        switch (encryption)
        {
            case SmtpEncryption.StartTLS:
                return SecureSocketOptions.StartTls;
            case SmtpEncryption.SSL:
                return SecureSocketOptions.SslOnConnect;
            default:
                return SecureSocketOptions.None;
        }
    }

    private MimeMessage CreateMailMessage(MailMessage notification)
    {
        var message = new MimeMessage { Subject = notification.Subject };
        var builder = new BodyBuilder();

        if (!String.IsNullOrEmpty(notification.To))
            message.To.AddRange(InternetAddressList.Parse(notification.To));

        if (!String.IsNullOrEmpty(notification.From))
            message.From.AddRange(InternetAddressList.Parse(notification.From));
        else
            message.From.AddRange(InternetAddressList.Parse(_emailOptions.SmtpFrom));

        if (!String.IsNullOrEmpty(notification.Body))
            builder.HtmlBody = notification.Body;

        message.Body = builder.ToMessageBody();
        return message;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (_lastSuccessfulConnection.IsAfter(SystemClock.UtcNow.Subtract(TimeSpan.FromMinutes(5))))
            return HealthCheckResult.Healthy();

        string? host = _emailOptions.SmtpHost;
        if (String.IsNullOrEmpty(host))
            return HealthCheckResult.Unhealthy("Email is not configured");

        var sw = Stopwatch.StartNew();

        try
        {
            using var client = new SmtpClient(new ExtensionsProtocolLogger(_logger));
            int port = _emailOptions.SmtpPort;
            var encryption = GetSecureSocketOption(_emailOptions.SmtpEncryption);

            await client.ConnectAsync(host, port, encryption).AnyContext();

            // Note: since we don't have an OAuth2 token, disable the XOAUTH2 authentication mechanism.
            client.AuthenticationMechanisms.Remove("XOAUTH2");

            string? user = _emailOptions.SmtpUser;
            if (!String.IsNullOrEmpty(user))
            {
                await client.AuthenticateAsync(user, _emailOptions.SmtpPassword).AnyContext();
            }

            await client.DisconnectAsync(true).AnyContext();

            _lastSuccessfulConnection = SystemClock.UtcNow;
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Email Not Working.", ex);
        }
        finally
        {
            sw.Stop();
            _logger.LogTrace("Checking email took {Duration:g}", sw.Elapsed);
        }

        return HealthCheckResult.Healthy();
    }
}
