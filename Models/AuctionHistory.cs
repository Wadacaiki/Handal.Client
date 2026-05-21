namespace Handal.Client.Models;

/// <summary>
/// История аукциона (запись после завершения)
/// </summary>
public class AuctionHistory
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// ID аукциона
    /// </summary>
    public string AuctionId { get; set; } = string.Empty;
    
    /// <summary>
    /// Информация об аукционе
    /// </summary>
    public Auction? Auction { get; set; }
    
    /// <summary>
    /// ID продавца
    /// </summary>
    public string SellerId { get; set; } = string.Empty;
    
    /// <summary>
    /// ID победителя (если есть)
    /// </summary>
    public string? WinnerId { get; set; }
    
    /// <summary>
    /// Информация о победителе
    /// </summary>
    public User? Winner { get; set; }
    
    /// <summary>
    /// Финальная цена
    /// </summary>
    public decimal FinalPrice { get; set; }
    
    /// <summary>
    /// Сумма, полученная продавцом
    /// </summary>
    public decimal SellerProceeds { get; set; }
    
    /// <summary>
    /// Сумма благотворительности (если был благотворительный аукцион)
    /// </summary>
    public decimal CharityAmount { get; set; } = 0m;
    
    /// <summary>
    /// Дата завершения
    /// </summary>
    public DateTime CompletedAt { get; set; } = DateTime.Now;
    
    /// <summary>
    /// Статус завершения
    /// </summary>
    public AuctionStatus FinalStatus { get; set; }
    
    /// <summary>
    /// Количество участников (уникальных ставящих)
    /// </summary>
    public int ParticipantsCount { get; set; }
    
    /// <summary>
    /// Количество ставок
    /// </summary>
    public int BidsCount { get; set; }
    
    /// <summary>
    /// Количество просмотров
    /// </summary>
    public int ViewsCount { get; set; }

    /// <summary>
    /// Список ID участников (всех, кто делал ставки)
    /// </summary>
    public List<string> ParticipantIds { get; set; } = new();
}
