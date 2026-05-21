namespace Handal.Client.Models;

/// <summary>
/// Сообщение в чате аукциона
/// </summary>
public class ChatMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// ID аукциона
    /// </summary>
    public string AuctionId { get; set; } = string.Empty;
    
    /// <summary>
    /// ID автора сообщения
    /// </summary>
    public string AuthorId { get; set; } = string.Empty;
    
    /// <summary>
    /// Информация об авторе
    /// </summary>
    public User? Author { get; set; }
    
    /// <summary>
    /// Текст сообщения
    /// </summary>
    public string Text { get; set; } = string.Empty;
    
    /// <summary>
    /// Время отправки
    /// </summary>
    public DateTime SentAt { get; set; } = DateTime.Now;
    
    /// <summary>
    /// Сообщение отмечено для модерации
    /// </summary>
    public bool IsReported { get; set; } = false;
    
    /// <summary>
    /// Скрыто модератором
    /// </summary>
    public bool IsHidden { get; set; } = false;
    
    /// <summary>
    /// Максимальная длина сообщения
    /// </summary>
    public const int MaxLength = 500;
}
