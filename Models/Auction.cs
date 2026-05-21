using System.ComponentModel.DataAnnotations;

namespace Handal.Client.Models;

/// <summary>
/// Статус аукциона
/// </summary>
public enum AuctionStatus
{
    /// <summary>Активный аукцион</summary>
    Active,
    /// <summary>Завершённый аукцион</summary>
    Completed,
    /// <summary>Не состоялся (нет ставок)</summary>
    NotHeld,
    /// <summary>Отменён</summary>
    Cancelled,
    /// <summary>На рассмотрении у администрации</summary>
    Pending
}

/// <summary>
/// Лот (товар на аукционе)
/// </summary>
public class Auction
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    [Required(ErrorMessage = "Название лота обязательно")]
    public string Title { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Описание обязательно")]
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Категория товара
    /// </summary>
    public string Category { get; set; } = string.Empty;
    
    /// <summary>
    /// ID продавца
    /// </summary>
    public string SellerId { get; set; } = string.Empty;
    
    /// <summary>
    /// Информация о продавце
    /// </summary>
    public User? Seller { get; set; }
    
    /// <summary>
    /// Стартовая цена
    /// </summary>
    public decimal StartPrice { get; set; }
    
    /// <summary>
    /// Текущая максимальная ставка
    /// </summary>
    public decimal CurrentBid { get; set; }
    
    /// <summary>
    /// ID пользователя с максимальной ставкой
    /// </summary>
    public string? CurrentWinnerId { get; set; }
    
    /// <summary>
    /// Финальная цена (если аукцион завершён)
    /// </summary>
    public decimal? FinalPrice { get; set; }
    
    /// <summary>
    /// Количество ставок
    /// </summary>
    public int BidsCount { get; set; } = 0;
    
    /// <summary>
    /// Количество просмотров
    /// </summary>
    public int ViewsCount { get; set; } = 0;
    
    /// <summary>
    /// URL главной фотографии
    /// </summary>
    public string Image { get; set; } = string.Empty;
    
    /// <summary>
    /// Дополнительные фотографии
    /// </summary>
    public List<string> Images { get; set; } = new();
    
    /// <summary>
    /// Дата начала аукциона
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.Now;
    
    /// <summary>
    /// Дата окончания аукциона
    /// </summary>
    public DateTime EndsAt { get; set; }
    
    /// <summary>
    /// Оставшееся время
    /// </summary>
    public TimeSpan TimeRemaining => EndsAt > DateTime.Now ? EndsAt - DateTime.Now : TimeSpan.Zero;
    
    /// <summary>
    /// Статус аукциона
    /// </summary>
    public AuctionStatus Status { get; set; } = AuctionStatus.Active;
    
    /// <summary>
    /// Отмечен как премиальный (горячий)
    /// </summary>
    public bool IsFeatured { get; set; } = false;
    
    /// <summary>
    /// Благотворительный аукцион
    /// </summary>
    public bool IsCharitable { get; set; } = false;
    
    /// <summary>
    /// Процент отчисления на благотворительность
    /// </summary>
    public decimal CharityPercent { get; set; } = 0m;
    
    /// <summary>
    /// История всех ставок
    /// </summary>
    public List<Bid> Bids { get; set; } = new();
    
    /// <summary>
    /// Сообщения в чате
    /// </summary>
    public List<ChatMessage> ChatMessages { get; set; } = new();

    /// <summary>
    /// Привязки тегов к лоту с отдельной модерацией
    /// </summary>
    public List<AuctionTagAssignment> TagAssignments { get; set; } = new();
    
    /// <summary>
    /// Количество продлений аукциона
    /// </summary>
    public int ExtensionCount { get; set; } = 0;
    
    /// <summary>
    /// Максимальное количество продлений
    /// </summary>
    public int MaxExtensions { get; set; } = 3;
    
    /// <summary>
    /// Минут для продления (если ставка в последние N минут)
    /// </summary>
    public int AutoExtendMinutes { get; set; } = 5;

    /// <summary>
    /// Цена, назначенная администратором на модерации
    /// </summary>
    public decimal? AppraisedPrice { get; set; }

    /// <summary>
    /// Время, когда администратор провёл оценку
    /// </summary>
    public DateTime? AppraisedAt { get; set; }

    /// <summary>
    /// Продавец подтвердил оценку администратора
    /// </summary>
    public bool SellerAcceptedAppraisal { get; set; } = false;

    /// <summary>
    /// Продавец отказался от оценки администратора
    /// </summary>
    public bool SellerRejectedAppraisal { get; set; } = false;

    /// <summary>
    /// Время решения продавца по оценке
    /// </summary>
    public DateTime? SellerDecisionAt { get; set; }

    /// <summary>
    /// Цена зафиксирована и больше не может быть изменена администрацией
    /// </summary>
    public bool IsPriceLockedBySellerDecision { get; set; } = false;
}
