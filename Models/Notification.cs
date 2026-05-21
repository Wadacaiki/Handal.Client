namespace Handal.Client.Models;

/// <summary>
/// Тип уведомления
/// </summary>
public enum NotificationType
{
    /// <summary>Повышение ставки</summary>
    BidIncreased,
    /// <summary>Вы победили в аукционе</summary>
    AuctionWon,
    /// <summary>Ваш аукцион завершён</summary>
    AuctionEnded,
    /// <summary>Новое сообщение в чате</summary>
    ChatMessage,
    /// <summary>Низкий баланс</summary>
    LowBalance,
    /// <summary>Подтверждение email</summary>
    EmailVerification,
    /// <summary>Оценка лота администрацией</summary>
    Appraisal,
    /// <summary>Системное уведомление</summary>
    System
}

/// <summary>
/// Уведомление пользователю
/// </summary>
public class Notification
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// ID получателя
    /// </summary>
    public string RecipientId { get; set; } = string.Empty;
    
    /// <summary>
    /// Тип уведомления
    /// </summary>
    public NotificationType Type { get; set; }
    
    /// <summary>
    /// Заголовок
    /// </summary>
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// Сообщение
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// Дополнительные данные (ID лота, ID пользователя и т.д.)
    /// </summary>
    public string? RelatedId { get; set; }
    
    /// <summary>
    /// Время создания
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    /// <summary>
    /// Прочитано
    /// </summary>
    public bool IsRead { get; set; } = false;
    
    /// <summary>
    /// Отправлено по email
    /// </summary>
    public bool EmailSent { get; set; } = false;
}
