using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace FleetTrack.Web.Reminders;

/// <summary>
/// SMTP settings, from configuration (never from source code). Works with any mail provider:
/// Microsoft 365 (smtp.office365.com:587), Gmail (smtp.gmail.com:587 with an app password), SendGrid, etc.
/// Locally, docker-compose runs Mailpit, which catches every email at http://localhost:8025.
/// </summary>
public class EmailOptions
{
    public string? Host { get; set; }
    public int Port { get; set; } = 587;

    /// <summary>Auto, None, StartTls or SslOnConnect. Use StartTls (or Auto) for real providers.</summary>
    public SecureSocketOptions Security { get; set; } = SecureSocketOptions.Auto;

    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? FromAddress { get; set; }
    public string FromName { get; set; } = "FleetTrack";

    /// <summary>The address people open FleetTrack on, e.g. https://fleettrack.example.co.za, used for links in emails.</summary>
    public string? AppBaseUrl { get; set; }

    /// <summary>The hour (South African time) after which the morning reminder email goes out.</summary>
    public int SendHour { get; set; } = 7;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(FromAddress);
}

public record EmailMessage(string To, string Subject, string HtmlBody, string TextBody);

public interface IMailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

public class SmtpMailSender(Microsoft.Extensions.Options.IOptions<EmailOptions> options) : IMailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (!settings.IsConfigured)
            throw new InvalidOperationException("Email isn't configured. Set Email__Host and Email__FromAddress.");

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress!));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        // Both an HTML and a plain-text version, so it reads well in every mail app.
        mime.Body = new BodyBuilder { HtmlBody = message.HtmlBody, TextBody = message.TextBody }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(settings.Host!, settings.Port, settings.Security, cancellationToken);
        if (!string.IsNullOrEmpty(settings.Username))
            await client.AuthenticateAsync(settings.Username, settings.Password ?? "", cancellationToken);
        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }
}
