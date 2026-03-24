using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Mail;

namespace ChoThueXe.Services;

public class EmailService
{
    private readonly IConfiguration _configuration;

    public EmailService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task SendOtpEmailAsync(string email, string otpCode)
    {
        var smtpSection = _configuration.GetSection("Smtp");
        var host = smtpSection.GetValue<string>("Host") ?? throw new InvalidOperationException("SMTP host is not configured.");
        var port = smtpSection.GetValue<int>("Port");
        var user = smtpSection.GetValue<string>("Username") ?? throw new InvalidOperationException("SMTP username is not configured.");
        var pass = smtpSection.GetValue<string>("Password") ?? throw new InvalidOperationException("SMTP password is not configured.");
        var from = smtpSection.GetValue<string>("From") ?? throw new InvalidOperationException("SMTP from address is not configured.");

        using var client = new SmtpClient(host, port)
        {
            Credentials = new NetworkCredential(user, pass),
            EnableSsl = smtpSection.GetValue<bool>("EnableSsl", true)
        };

        var message = new MailMessage(from, email)
        {
            Subject = "OTP Hafuan Xác Thực",
            Body = $"Ma OTP cua ban la: {otpCode} (hieu luc 15 phut)",
            IsBodyHtml = false
        };

        await client.SendMailAsync(message);
    }
}