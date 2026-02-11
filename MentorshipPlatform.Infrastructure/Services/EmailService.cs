using System.Net;
using System.Net.Mail;
using MentorshipPlatform.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MentorshipPlatform.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        IOptions<EmailOptions> options,
        ILogger<EmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendBookingConfirmationAsync(
        string to,
        string mentorName,
        DateTime startAt,
        CancellationToken cancellationToken = default)
    {
        var subject = "Rezervasyonunuz Onaylandı! 🎉";
        var body = $@"
            <html>
            <body style='font-family: Arial, sans-serif;'>
                <h2>Merhaba!</h2>
                <p>Rezervasyonunuz başarıyla oluşturuldu.</p>
                <div style='background-color: #f3f4f6; padding: 20px; border-radius: 8px; margin: 20px 0;'>
                    <p><strong>Mentör:</strong> {mentorName}</p>
                    <p><strong>Tarih:</strong> {startAt:dd MMMM yyyy}</p>
                    <p><strong>Saat:</strong> {startAt:HH:mm}</p>
                </div>
                <p>Ders zamanı geldiğinde size hatırlatma göndereceğiz.</p>
                <p>İyi dersler! 📚</p>
                <hr>
                <p style='font-size: 12px; color: #6b7280;'>
                    Bu otomatik bir mesajdır, lütfen yanıtlamayın.
                </p>
            </body>
            </html>
        ";

        await SendEmailAsync(to, subject, body, cancellationToken);
    }

    public async Task SendBookingReminderAsync(
        string to,
        string mentorName,
        DateTime startAt,
        string timeframe,
        CancellationToken cancellationToken = default)
    {
        var subject = timeframe == "10m" 
            ? "⏰ Dersiniz 10 dakika sonra başlıyor!" 
            : $"📅 Dersiniz yaklaşıyor - {timeframe}";

        var urgency = timeframe == "10m" 
            ? "Dersiniz <strong>10 dakika</strong> sonra başlıyor! Hazır olun." 
            : $"Dersinizi hatırlatmak istedik. {timeframe} kaldı.";

        var body = $@"
            <html>
            <body style='font-family: Arial, sans-serif;'>
                <h2>Merhaba!</h2>
                <p>{urgency}</p>
                <div style='background-color: #f3f4f6; padding: 20px; border-radius: 8px; margin: 20px 0;'>
                    <p><strong>Mentör:</strong> {mentorName}</p>
                    <p><strong>Tarih:</strong> {startAt:dd MMMM yyyy}</p>
                    <p><strong>Saat:</strong> {startAt:HH:mm}</p>
                </div>
                <p>
                    <a href='https://yourdomain.com/classroom/{startAt:yyyyMMddHHmm}' 
                       style='background-color: #2563eb; color: white; padding: 12px 24px; 
                              text-decoration: none; border-radius: 6px; display: inline-block;'>
                        Derse Katıl
                    </a>
                </p>
                <p>Görüşmek üzere! 👋</p>
            </body>
            </html>
        ";

        await SendEmailAsync(to, subject, body, cancellationToken);
    }

    public async Task SendBookingCancelledAsync(
        string to,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var subject = "Rezervasyon İptal Edildi";
        var body = $@"
            <html>
            <body style='font-family: Arial, sans-serif;'>
                <h2>Rezervasyon İptal Edildi</h2>
                <p>Rezervasyonunuz iptal edildi.</p>
                <div style='background-color: #fef2f2; padding: 20px; border-radius: 8px; margin: 20px 0;'>
                    <p><strong>İptal Sebebi:</strong> {reason}</p>
                </div>
                <p>İade işleminiz başlatıldı ve kısa süre içinde hesabınıza yansıyacak.</p>
                <p>Başka bir zamanda görüşmek üzere!</p>
            </body>
            </html>
        ";

        await SendEmailAsync(to, subject, body, cancellationToken);
    }

    public async Task SendVerificationApprovedAsync(
        string to,
        string verificationType,
        CancellationToken cancellationToken = default)
    {
        var subject = "✅ Doğrulamanız Onaylandı!";
        var body = $@"
            <html>
            <body style='font-family: Arial, sans-serif;'>
                <h2>Harika Haber!</h2>
                <p><strong>{verificationType}</strong> doğrulamanız onaylandı! 🎉</p>
                <p>Profilinizde artık doğrulama rozeti görünecek.</p>
                <p>Bu, daha fazla danışan çekmenize yardımcı olacak.</p>
                <p>
                    <a href='https://yourdomain.com/mentor/dashboard' 
                       style='background-color: #10b981; color: white; padding: 12px 24px; 
                              text-decoration: none; border-radius: 6px; display: inline-block;'>
                        Dashboard'a Git
                    </a>
                </p>
            </body>
            </html>
        ";

        await SendEmailAsync(to, subject, body, cancellationToken);
    }

    public async Task SendWelcomeEmailAsync(
        string to,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        var subject = "MentorHub'a Hoş Geldin! 🎓";
        var body = $@"
            <html>
            <body style='font-family: Arial, sans-serif;'>
                <h2>Merhaba {displayName}!</h2>
                <p>MentorHub ailesine hoş geldin! 🎉</p>
                <p>Artık derece yapmış mentörlerden birebir mentorluk alabilirsin.</p>
                <div style='background-color: #f3f4f6; padding: 20px; border-radius: 8px; margin: 20px 0;'>
                    <h3>İlk Adımlar:</h3>
                    <ul>
                        <li>✅ Profilini tamamla</li>
                        <li>✅ Sana uygun mentörleri keşfet</li>
                        <li>✅ İlk rezervasyonunu yap</li>
                    </ul>
                </div>
                <p>
                    <a href='https://yourdomain.com/mentors' 
                       style='background-color: #2563eb; color: white; padding: 12px 24px; 
                              text-decoration: none; border-radius: 6px; display: inline-block;'>
                        Mentörleri Keşfet
                    </a>
                </p>
                <p>Başarılar dileriz! 🚀</p>
            </body>
            </html>
        ";

        await SendEmailAsync(to, subject, body, cancellationToken);
    }

    private async Task SendEmailAsync(
        string to,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(_options.FromEmail, _options.FromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            message.To.Add(to);

            using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
            {
                Credentials = new NetworkCredential(_options.SmtpUsername, _options.SmtpPassword),
                EnableSsl = true
            };

            await client.SendMailAsync(message, cancellationToken);
            
            _logger.LogInformation("Email sent to {To}: {Subject}", to, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}", to);
            // Don't throw - email failures shouldn't break the flow
        }
    }
}
public class EmailOptions
{
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; }
    public string SmtpUsername { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
}