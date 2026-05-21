using Handal.Client.Models;

namespace Handal.Client.Services;

/// <summary>
/// Сервис для работы с чатом аукциона
/// </summary>
public class ChatService
{
    private List<ChatMessage> _messages = new();
    
    public List<ChatMessage> GetAllMessages()
    {
        return _messages;
    }

    public void AddMessage(ChatMessage message)
    {
        _messages.Add(message);
    }
    
    /// <summary>
    /// Получить сообщения аукциона
    /// </summary>
    public List<ChatMessage> GetAuctionMessages(string auctionId)
    {
        return _messages
            .Where(m => m.AuctionId == auctionId && !m.IsHidden)
            .OrderBy(m => m.SentAt)
            .ToList();
    }
    
    /// <summary>
    /// Получить количество сообщений
    /// </summary>
    public int GetMessageCount(string auctionId)
    {
        return _messages.Count(m => m.AuctionId == auctionId && !m.IsHidden);
    }
    
    /// <summary>
    /// Отправить сообщение
    /// </summary>
    public (bool success, string message, ChatMessage? chatMessage) SendMessage(
        string auctionId,
        string authorId,
        string text)
    {
        text = text?.Trim() ?? string.Empty;

        // Проверка длины сообщения
        if (string.IsNullOrWhiteSpace(text))
            return (false, "Сообщение не может быть пустым", null);
        
        if (text.Length > ChatMessage.MaxLength)
            return (false, $"Сообщение не может быть больше {ChatMessage.MaxLength} символов", null);
        
        // Проверка rate limit (не более 3 сообщений в минуту)
        var recentMessages = _messages
            .Where(m => m.AuthorId == authorId && m.AuctionId == auctionId)
            .Where(m => DateTime.Now - m.SentAt < TimeSpan.FromMinutes(1))
            .ToList();
        
        if (recentMessages.Count >= 3)
            return (false, "Вы отправляете слишком много сообщений. Подождите минуту", null);
        
        var chatMessage = new ChatMessage
        {
            AuctionId = auctionId,
            AuthorId = authorId,
            Text = text,
            SentAt = DateTime.Now,
            IsReported = false,
            IsHidden = false
        };
        
        _messages.Add(chatMessage);
        return (true, "Сообщение отправлено", chatMessage);
    }
    
    /// <summary>
    /// Отметить сообщение для модерации
    /// </summary>
    public bool ReportMessage(string messageId)
    {
        var message = _messages.FirstOrDefault(m => m.Id == messageId);
        if (message == null)
            return false;
        
        message.IsReported = true;
        return true;
    }
    
    /// <summary>
    /// Скрыть сообщение (модератор)
    /// </summary>
    public bool HideMessage(string messageId)
    {
        var message = _messages.FirstOrDefault(m => m.Id == messageId);
        if (message == null)
            return false;
        
        message.IsHidden = true;
        return true;
    }
    
    /// <summary>
    /// Удалить сообщение
    /// </summary>
    public bool DeleteMessage(string messageId)
    {
        var message = _messages.FirstOrDefault(m => m.Id == messageId);
        if (message == null)
            return false;
        
        return _messages.Remove(message);
    }
    
    /// <summary>
    /// Получить сообщения, отмеченные для модерации
    /// </summary>
    public List<ChatMessage> GetReportedMessages()
    {
        return _messages.Where(m => m.IsReported && !m.IsHidden).ToList();
    }
}
