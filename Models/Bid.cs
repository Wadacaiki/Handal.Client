namespace Handal.Client.Models;

/// <summary>
/// Ставка на аукционе
/// </summary>
public class Bid
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// ID аукциона
    /// </summary>
    public string AuctionId { get; set; } = string.Empty;
    
    /// <summary>
    /// ID пользователя (делающего ставку)
    /// </summary>
    public string UserId { get; set; } = string.Empty;
    
    /// <summary>
    /// Информация о пользователе
    /// </summary>
    public User? User { get; set; }
    
    /// <summary>
    /// Сумма ставки
    /// </summary>
    public decimal Amount { get; set; }
    
    /// <summary>
    /// Зарезервированная сумма
    /// </summary>
    public decimal ReservedAmount { get; set; }
    
    /// <summary>
    /// Время ставки
    /// </summary>
    public DateTime PlacedAt { get; set; } = DateTime.Now;
    
    /// <summary>
    /// Ставка активна (не отменена)
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Это победившая ставка
    /// </summary>
    public bool IsWinning { get; set; } = false;
}
