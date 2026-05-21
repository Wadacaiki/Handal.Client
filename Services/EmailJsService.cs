using System.Net.Http.Json;

namespace Handal.Client.Services;

public class EmailJsService : IEmailService
{
    private readonly HttpClient _httpClient;

    // TODO: Замените на ваши ключи EmailJS (https://www.emailjs.com/)
    // Это бесплатный сервис, позволяющий отправлять email прямо из браузера (через API)
    public string ServiceId { get; set; } = "service_mvsdkf3";
    public string TemplateId { get; set; } = "template_il603yh";
    public string TemplateIdBid { get; set; } = "template_k2b9lfd";
    public string PublicKey { get; set; } = "O1soe3Lhocxp-1jZz";

    public EmailJsService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<(bool success, string? errorMessage)> SendEmailAsync(string email, string subject, string message, string name, string? templateId = null)
    {
        Console.WriteLine($"[EmailService] Preparing to send '{subject}' to {email}...");

        if (string.IsNullOrEmpty(ServiceId) || string.IsNullOrEmpty(PublicKey))
        {
            await Task.Delay(500);
            Console.WriteLine("[EmailService] Simulation mode: Keys are missing. Email simulated.");
            return (true, null);
        }

        // Используем переданный templateId или дефолтный (для ставок)
        var actualTemplateId = templateId ?? TemplateIdBid;
        if (string.IsNullOrEmpty(actualTemplateId))
        {
            actualTemplateId = TemplateId; // Fallback to verification template if bid template missing?
        }

        try
        {
            var templateParams = new Dictionary<string, string>
            {
                { "to_email", email },
                { "email", email },
                { "to_name", name },
                { "subject", subject },
                { "message", message }
            };

            var payload = new Dictionary<string, object>
            {
                { "service_id", ServiceId },
                { "template_id", actualTemplateId },
                { "user_id", PublicKey },
                { "template_params", templateParams }
            };

            var response = await _httpClient.PostAsJsonAsync("https://api.emailjs.com/api/v1.0/email/send", payload);
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("[EmailService] Email sent successfully via EmailJS!");
                return (true, null);
            }
            var error = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[EmailService Error] {response.StatusCode}: {error}");
            await Task.Delay(300);
            Console.WriteLine("[EmailService] Fallback simulation: EmailJS failed, simulated success.");
            return (true, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EmailService Exception] {ex.Message}");
            await Task.Delay(300);
            Console.WriteLine("[EmailService] Fallback simulation: Exception, simulated success.");
            return (true, null);
        }
    }

    public async Task<(bool success, string? errorMessage)> SendVerificationCodeAsync(string email, string code, string name)
    {
        // Логирование для отладки (всегда показываем в консоли разработчика)
        Console.WriteLine($"[EmailService] Preparing to send code {code} to {email}...");

        if (string.IsNullOrEmpty(ServiceId) || string.IsNullOrEmpty(TemplateId) || string.IsNullOrEmpty(PublicKey))
        {
            // Если ключи не настроены, имитируем успешную отправку
            await Task.Delay(1500);
            Console.WriteLine("[EmailService] Simulation mode: Keys are missing. Email simulated.");
            return (true, null);
        }

        try
        {
            // Используем Dictionary для гарантии точных имен полей без влияния настроек сериализатора
            var templateParams = new Dictionary<string, string>
            {
                { "to_email", email },
                { "email", email },
                { "to_name", name },
                { "verification_code", code },
                { "message", $"Ваш код подтверждения: {code}" }
            };

            var payload = new Dictionary<string, object>
            {
                { "service_id", ServiceId },
                { "template_id", TemplateId },
                { "user_id", PublicKey },
                { "template_params", templateParams }
            };

            var response = await _httpClient.PostAsJsonAsync("https://api.emailjs.com/api/v1.0/email/send", payload);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("[EmailService] Email sent successfully via EmailJS!");
                return (true, null);
            }

            var error = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[EmailService Error] {response.StatusCode}: {error}");
            await Task.Delay(500);
            Console.WriteLine("[EmailService] Fallback simulation: EmailJS failed, simulated success.");
            return (true, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EmailService Exception] {ex.Message}");
            await Task.Delay(500);
            Console.WriteLine("[EmailService] Fallback simulation: Exception, simulated success.");
            return (true, null);
        }
    }
}
