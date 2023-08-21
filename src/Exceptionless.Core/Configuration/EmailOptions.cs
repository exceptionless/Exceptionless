﻿using Exceptionless.Core.Extensions;
using Exceptionless.Core.Utility;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Exceptionless.Core.Configuration;

public class EmailOptions
{
    public bool EnableDailySummary { get; internal set; }

    /// <summary>
    /// All emails that do not match the AllowedOutboundAddresses will be sent to this address in QA mode
    /// </summary>
    public string? TestEmailAddress { get; internal set; }

    /// <summary>
    /// Email addresses that match this comma delimited list of domains and email addresses will be allowed to be sent out in QA mode
    /// </summary>
    public List<string> AllowedOutboundAddresses { get; internal set; } = null!;

    public string? SmtpFrom { get; internal set; }

    public string? SmtpHost { get; internal set; }

    public int SmtpPort { get; internal set; }

    [JsonConverter(typeof(StringEnumConverter))]
    public SmtpEncryption SmtpEncryption { get; internal set; }

    public string? SmtpUser { get; internal set; }

    public string? SmtpPassword { get; internal set; }

    public static EmailOptions ReadFromConfiguration(IConfiguration config, AppOptions appOptions)
    {
        var options = new EmailOptions();

        options.EnableDailySummary = config.GetValue(nameof(options.EnableDailySummary), appOptions.AppMode == AppMode.Production);
        options.AllowedOutboundAddresses = config.GetValueList(nameof(options.AllowedOutboundAddresses)).Select(v => v.ToLowerInvariant()).ToList();
        options.TestEmailAddress = config.GetValue(nameof(options.TestEmailAddress), appOptions.AppMode == AppMode.Development ? "test@localhost" : null);

        string? emailConnectionString = config.GetConnectionString("Email");
        if (!String.IsNullOrEmpty(emailConnectionString))
        {
            var uri = new SmtpUri(emailConnectionString);
            options.SmtpHost = uri.Host;
            options.SmtpPort = uri.Port;
            options.SmtpUser = uri.User;
            options.SmtpPassword = uri.Password;
        }

        options.SmtpFrom = config.GetValue(nameof(options.SmtpFrom), appOptions.AppMode == AppMode.Development ? "Exceptionless <noreply@localhost>" : null);
        options.SmtpEncryption = config.GetValue(nameof(options.SmtpEncryption), GetDefaultSmtpEncryption(options.SmtpPort));

        if (String.IsNullOrWhiteSpace(options.SmtpUser) != String.IsNullOrWhiteSpace(options.SmtpPassword))
            throw new ArgumentException("Must specify both the SmtpUser and the SmtpPassword, or neither.");

        return options;
    }

    private static SmtpEncryption GetDefaultSmtpEncryption(int port)
    {
        return port switch
        {
            465 => SmtpEncryption.SSL,
            587 => SmtpEncryption.StartTLS,
            2525 => SmtpEncryption.StartTLS,
            _ => SmtpEncryption.None
        };
    }
}

public enum SmtpEncryption
{
    None,
    StartTLS,
    SSL
}
