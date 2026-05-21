using System.Threading.Tasks;

namespace Handal.Client.Services;

public interface IEmailService
{
    /// <summary>
    /// Отправить код подтверждения. Возвращает (success, errorMessage)
    /// </summary>
    Task<(bool success, string? errorMessage)> SendVerificationCodeAsync(string email, string code, string name);

    /// <summary>
    /// Отправить простое письмо (тема + сообщение). Возвращает (success, errorMessage)
    /// </summary>
    Task<(bool success, string? errorMessage)> SendEmailAsync(string email, string subject, string message, string name, string? templateId = null);
}
