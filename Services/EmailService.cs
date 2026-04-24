using MailKit.Net.Smtp;
using MimeKit;

namespace PersonelTakip.Services;

public class EmailService
{
    private readonly EmailConfig _config;

    public EmailService()
    {
        _config = AppConfiguration.Instance.Email;
    }



    public async Task RolDegistirmeBildirimiGonderAsync(string toEmail, string kullaniciAdi, string yeniRol)
    {
        var htmlContent = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                <div style='background-color: #8B0000; padding: 20px; text-align: center;'>
                    <h1 style='color: white; margin: 0;'>Personel Takip Sistemi</h1>
                </div>
                <div style='padding: 30px; background-color: #f9f9f9;'>
                    <h2 style='color: #333;'>Merhaba {kullaniciAdi},</h2>
                    <p style='color: #666; font-size: 16px;'>
                        Hesabınızın rolü <strong>{yeniRol}</strong> olarak güncellenmiştir.
                    </p>
                    <p style='color: #666; font-size: 16px;'>
                        Yeni rolünüzle sisteme giriş yapabilirsiniz.
                    </p>
                </div>
                <div style='padding: 20px; text-align: center; background-color: #eee; font-size: 12px; color: #666;'>
                    © {DateTime.Now.Year} Personel Takip Sistemi
                </div>
            </div>";

        await SendEmailAsync(toEmail, "Hesap Rolünüz Güncellendi", htmlContent);
    }

    public async Task KullaniciOlusturmaBildirimiGonderAsync(string toEmail, string kullaniciAdi, string ilkSifre)
    {
        var htmlContent = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                <div style='background-color: #8B0000; padding: 20px; text-align: center;'>
                    <h1 style='color: white; margin: 0;'>Personel Takip Sistemi</h1>
                </div>
                <div style='padding: 30px; background-color: #f9f9f9;'>
                    <h2 style='color: #333;'>Hoş Geldiniz,</h2>
                    <p style='color: #666; font-size: 16px;'>
                        Personel Takip Sistemi için hesabınız oluşturuldu.
                    </p>
                    <table style='margin: 20px 0; border-collapse: collapse;'>
                        <tr>
                            <td style='padding: 10px; border: 1px solid #ddd; font-weight: bold;'>Kullanıcı Adı:</td>
                            <td style='padding: 10px; border: 1px solid #ddd;'>{kullaniciAdi}</td>
                        </tr>
                        <tr>
                            <td style='padding: 10px; border: 1px solid #ddd; font-weight: bold;'>Email:</td>
                            <td style='padding: 10px; border: 1px solid #ddd;'>{toEmail}</td>
                        </tr>
                        <tr>
                            <td style='padding: 10px; border: 1px solid #ddd; font-weight: bold;'>İlk Parola:</td>
                            <td style='padding: 10px; border: 1px solid #ddd;'>{ilkSifre}</td>
                        </tr>
                    </table>
                    <p style='color: #999; font-size: 14px;'>
                        İlk girişinizden sonra parolanızı değiştirmeniz önerilir.
                    </p>
                </div>
                <div style='padding: 20px; text-align: center; background-color: #eee; font-size: 12px; color: #666;'>
                    © {DateTime.Now.Year} Personel Takip Sistemi
                </div>
            </div>";

        await SendEmailAsync(toEmail, "Hesabınız Oluşturuldu", htmlContent);
    }

    private async Task SendEmailAsync(string to, string subject, string htmlBody)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_config.FromName, _config.FromEmail));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;

            message.Body = new TextPart("html")
            {
                Text = htmlBody
            };

            using var client = new SmtpClient();
            // SSL/TLS Sertifika doğrulama hatalarını görmezden gelmek için (sadece test ortamında gerekirse)
            // client.ServerCertificateValidationCallback = (s, c, h, e) => true;

            await client.ConnectAsync(_config.SmtpServer, _config.Port, MailKit.Security.SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_config.Username, _config.Password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            // Hatayı fırlatalım ki ViewModel üzerinden kullanıcıya bildirilebilsin
            throw new Exception($"Email gönderilirken bir hata oluştu: {ex.Message}");
        }
    }
}
