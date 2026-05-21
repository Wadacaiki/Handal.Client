using System.ComponentModel.DataAnnotations;

namespace Handal.Client.Models;

public class User
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    [Required(ErrorMessage = "Email обязателен")]
    [EmailAddress(ErrorMessage = "Некорректный формат email")]
    public string Email { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Пароль обязателен")]
    [MinLength(6, ErrorMessage = "Пароль должен быть минимум 6 символов")]
    public string Password { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Имя обязательно")]
    public string FullName { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Возраст обязателен")]
    public DateTime DateOfBirth { get; set; }
    
    /// <summary>
    /// Проверка возраста (должен быть старше 18 лет)
    /// </summary>
    public int Age
    {
        get
        {
            var today = DateTime.Today;
            var age = today.Year - DateOfBirth.Year;
            if (DateOfBirth.Date > today.AddYears(-age))
                age--;
            return age;
        }
    }
    
    public bool IsAgeVerified => Age >= 18;
    
    /// <summary>
    /// Условная валюта пользователя
    /// </summary>
    public decimal Balance { get; set; } = 0m;
    
    /// <summary>
    /// Зарезервированная сумма (активные ставки)
    /// </summary>
    public decimal ReservedBalance { get; set; } = 0m;
    
    /// <summary>
    /// Доступный баланс
    /// </summary>
    public decimal AvailableBalance => Balance - ReservedBalance;
    
    /// <summary>
    /// Привязана ли банковская карта
    /// </summary>
    public bool IsCardLinked { get; set; } = false;
    
    /// <summary>
    /// Маска карты (последние 4 цифры)
    /// </summary>
    public string? CardMask { get; set; }
    
    /// <summary>
    /// Email подтвержден
    /// </summary>
    public bool IsEmailVerified { get; set; } = false;

    /// <summary>
    /// Пользователь является администратором
    /// </summary>
    public bool IsAdmin { get; set; } = false;

    /// <summary>
    /// Код подтверждения (для демо)
    /// </summary>
    public string? VerificationCode { get; set; }
    
    /// <summary>
    /// Согласие на получение уведомлений
    /// </summary>
    public bool ReceiveNotifications { get; set; } = true;
    
    /// <summary>
    /// Дата регистрации
    /// </summary>
    public DateTime RegisteredAt { get; set; } = DateTime.Now;
    
    /// <summary>
    /// Последний вход
    /// </summary>
    public DateTime? LastLogin { get; set; }
    
    /// <summary>
    /// Избранные аукционы (ID)
    /// </summary>
    public List<string> FavoriteAuctionIds { get; set; } = new();
    
    /// <summary>
    /// Рейтинг пользователя
    /// </summary>
    public decimal Rating { get; set; } = 0m;
    
    /// <summary>
    /// Количество завершённых покупок
    /// </summary>
    public int PurchaseCount { get; set; } = 0;
    
    /// <summary>
    /// Количество завершённых продаж
    /// </summary>
    public int SalesCount { get; set; } = 0;

    /// <summary>
    /// Сообщение об ограничении от админа
    /// </summary>
    public string? RestrictionMessage { get; set; }

    /// <summary>
    /// Дата последнего ограничения
    /// </summary>
    public DateTime? LastRestrictionDate { get; set; }

    /// <summary>
    /// Активно ли ограничение (на сегодня)
    /// </summary>
    public bool IsRestricted => LastRestrictionDate.HasValue && LastRestrictionDate.Value.Date == DateTime.Today;

    public List<SavedTagFilterSet>? SavedTagFilters { get; set; } = new();
}

public class SavedTagFilterSet
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public List<string>? Tags { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public int UsageCount { get; set; } = 0;
}
