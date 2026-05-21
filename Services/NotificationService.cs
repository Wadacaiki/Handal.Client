using Handal.Client.Models;

namespace Handal.Client.Services;

/// <summary>
/// Сервис для системы уведомлений
/// </summary>
public class NotificationService
{
    private List<Notification> _notifications = new();

    /// <summary>
    /// Получить уведомления пользователя
    /// </summary>
    public List<Notification> GetUserNotifications(string userId, bool unreadOnly = false)
    {
        var query = _notifications.Where(n => n.RecipientId == userId);

        if (unreadOnly)
            query = query.Where(n => !n.IsRead);

        return query.OrderByDescending(n => n.CreatedAt).ToList();
    }

    /// <summary>
    /// Получить количество непрочитанных уведомлений
    /// </summary>
    public int GetUnreadCount(string userId)
    {
        return _notifications.Count(n => n.RecipientId == userId && !n.IsRead);
    }

    public List<Notification> GetAllNotifications()
    {
        return _notifications;
    }

    /// <summary>
    /// Уведомить администратора о новом лоте
    /// </summary>
    public void NotifyAdminNewAuction(string sellerName, string auctionTitle)
    {
        // Внутреннее уведомление для админа (ID "admin-internal")
        CreateNotification(
            "admin-internal",
            NotificationType.System,
            "Новый лот на модерацию",
            $"Пользователь {sellerName} отправил лот \"{auctionTitle}\" на рассмотрение.",
            null);
    }

    /// <summary>
    /// Создать уведомление о создании аукциона
    /// </summary>
    public void NotifyAuctionCreated(string recipientId, string auctionTitle)
    {
        CreateNotification(
            recipientId,
            NotificationType.System,
            "Лот отправлен на модерацию",
            $"Ваш лот \"{auctionTitle}\" успешно отправлен на рассмотрение администрации. Вы получите уведомление после проверки.",
            null);
    }

    /// <summary>
    /// Уведомить продавца, что лот оценен администрацией и требуется решение
    /// </summary>
    public void NotifyAuctionAppraised(string recipientId, string auctionId, string auctionTitle, decimal appraisedPrice)
    {
        CreateNotification(
            recipientId,
            NotificationType.Appraisal,
            "Лот оценен администрацией",
            $"Ваш товар \"{auctionTitle}\" оценили на {appraisedPrice:N0} грошей. Подтвердите или отклоните оценку.",
            auctionId);
    }

    /// <summary>
    /// Создать уведомление о регистрации
    /// </summary>
    public void NotifyRegistration(string recipientId, string userName)
    {
        CreateNotification(
            recipientId,
            NotificationType.System,
            "Добро пожаловать!",
            $"Приветствуем, {userName}! Вы успешно зарегистрировались в системе Handal.",
            null);
    }

    /// <summary>
    /// Создать уведомление о повышении ставки
    /// </summary>
    public void NotifyBidIncreased(string recipientId, string auctionId, string auctionTitle, decimal newBid)
    {
        CreateNotification(
            recipientId,
            NotificationType.BidIncreased,
            "Ставка повышена",
            $"На аукцион \"{auctionTitle}\" поступила новая ставка: {newBid} гроши",
            auctionId);
    }

    /// <summary>
    /// Создать уведомление о победе в аукционе
    /// </summary>
    public void NotifyAuctionWon(string recipientId, string auctionId, string auctionTitle, decimal amount)
    {
        CreateNotification(
            recipientId,
            NotificationType.AuctionWon,
            "Вы победили!",
            $"Поздравляем! Вы выиграли аукцион \"{auctionTitle}\" на сумму {amount} гроши",
            auctionId);
    }

    /// <summary>
    /// Создать уведомление об окончании аукциона
    /// </summary>
    public void NotifyAuctionEnded(string recipientId, string auctionId, string auctionTitle, bool sold)
    {
        var message = sold
            ? $"Ваш аукцион \"{auctionTitle}\" завершён и продан"
            : $"Ваш аукцион \"{auctionTitle}\" завершён без продажи";

        CreateNotification(
            recipientId,
            NotificationType.AuctionEnded,
            "Аукцион завершён",
            message,
            auctionId);
    }

    /// <summary>
    /// Создать уведомление о новом сообщении в чате
    /// </summary>
    public void NotifyChatMessage(string recipientId, string auctionId, string auctionTitle, string authorName)
    {
        CreateNotification(
            recipientId,
            NotificationType.ChatMessage,
            "Новое сообщение в чате",
            $"{authorName} отправил сообщение на \"{auctionTitle}\"",
            auctionId);
    }

    /// <summary>
    /// Создать уведомление о низком балансе
    /// </summary>
    public void NotifyLowBalance(string recipientId, decimal currentBalance)
    {
        CreateNotification(
            recipientId,
            NotificationType.LowBalance,
            "Низкий баланс",
            $"Ваш баланс ниже 1000 гроши. Текущий баланс: {currentBalance} гроши",
            null);
    }

    /// <summary>
    /// Создать уведомление о подтверждении email
    /// </summary>
    public void NotifyEmailVerification(string recipientId)
    {
        CreateNotification(
            recipientId,
            NotificationType.EmailVerification,
            "Подтверждение email",
            "Пожалуйста, подтвердите ваш адрес email",
            null);
    }

    /// <summary>
    /// Создать системное уведомление
    /// </summary>
    public void CreateSystemNotification(string recipientId, string title, string message)
    {
        CreateNotification(recipientId, NotificationType.System, title, message, null);
    }

    /// <summary>
    /// Отметить уведомление как прочитанное
    /// </summary>
    public bool MarkAsRead(string notificationId)
    {
        var notification = _notifications.FirstOrDefault(n => n.Id == notificationId);
        if (notification == null)
            return false;

        notification.IsRead = true;
        return true;
    }

    /// <summary>
    /// Отметить все уведомления как прочитанные
    /// </summary>
    public void MarkAllAsRead(string userId)
    {
        var userNotifications = _notifications.Where(n => n.RecipientId == userId && !n.IsRead);
        foreach (var notification in userNotifications)
        {
            notification.IsRead = true;
        }
    }

    /// <summary>
    /// Удалить уведомление
    /// </summary>
    public bool DeleteNotification(string notificationId)
    {
        var notification = _notifications.FirstOrDefault(n => n.Id == notificationId);
        if (notification == null)
            return false;

        return _notifications.Remove(notification);
    }

    /// <summary>
    /// Создать уведомление (внутренний метод)
    /// </summary>
    private void CreateNotification(
        string recipientId,
        NotificationType type,
        string title,
        string message,
        string? relatedId)
    {
        var notification = new Notification
        {
            RecipientId = recipientId,
            Type = type,
            Title = title,
            Message = message,
            RelatedId = relatedId,
            CreatedAt = DateTime.Now,
            IsRead = false
        };

        _notifications.Add(notification);
    }
}
