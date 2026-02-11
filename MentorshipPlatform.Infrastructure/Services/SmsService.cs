using MentorshipPlatform.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace MentorshipPlatform.Infrastructure.Services;

public class SmsService : ISmsService
{
    private readonly TwilioOptions _options;
    private readonly ILogger<SmsService> _logger;

    public SmsService(
        IOptions<TwilioOptions> options,
        ILogger<SmsService> logger)
    {
        _options = options.Value;
        _logger = logger;
        
        TwilioClient.Init(_options.AccountSid, _options.AuthToken);
    }

    public async Task SendBookingReminderSmsAsync(
        string phoneNumber,
        string mentorName,
        DateTime startAt,
        CancellationToken cancellationToken = default)
    {
        var message = $"Hatırlatma: {mentorName} ile dersiniz {startAt:HH:mm}'de başlıyor! MentorHub";
        
        await SendSmsAsync(phoneNumber, message, cancellationToken);
    }

    public async Task SendVerificationCodeAsync(
        string phoneNumber,
        string code,
        CancellationToken cancellationToken = default)
    {
        var message = $"MentorHub doğrulama kodunuz: {code}. Bu kodu kimseyle paylaşmayın.";
        
        await SendSmsAsync(phoneNumber, message, cancellationToken);
    }

    private async Task SendSmsAsync(
        string phoneNumber,
        string message,
        CancellationToken cancellationToken)
    {
        try
        {
            var messageResource = await MessageResource.CreateAsync(
                to: new PhoneNumber(phoneNumber),
                from: new PhoneNumber(_options.PhoneNumber),
                body: message
            );

            _logger.LogInformation("SMS sent to {PhoneNumber}: {MessageSid}", 
                phoneNumber, messageResource.Sid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SMS to {PhoneNumber}", phoneNumber);
            // Don't throw - SMS failures shouldn't break the flow
        }
    }
}
