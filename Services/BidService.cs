using Handal.Client.Models;

namespace Handal.Client.Services;

/// <summary>
/// Сервис для управления ставками
/// </summary>
public class BidService
{
    private readonly AuctionService _auctionService;
    private readonly UserService _userService;
    private readonly NotificationService _notificationService;
    private readonly IEmailService _emailService;
    private List<Bid> _bids = new();

    public BidService(AuctionService auctionService, UserService userService, NotificationService notificationService, IEmailService emailService)
    {
        _auctionService = auctionService;
        _userService = userService;
        _notificationService = notificationService;
        _emailService = emailService;
    }

    public void CancelAuctionBids(string auctionId)
    {
        var active = _bids.Where(b => b.AuctionId == auctionId && b.IsActive).ToList();
        foreach (var bid in active)
        {
            var user = _userService.GetUserById(bid.UserId);
            if (user != null)
            {
                // При отмене аукциона просто уменьшаем резерв, так как Balance — это общий баланс
                user.ReservedBalance -= bid.ReservedAmount;
                if (user.ReservedBalance < 0) user.ReservedBalance = 0;
            }
            bid.IsActive = false;
        }

        _userService.NotifyStateChanged();
    }

    public User? ResolveUser(string userId)
    {
        return _userService.GetUserById(userId);
    }

    /// <summary>
    /// Получить все ставки на аукцион
    /// </summary>
    public List<Bid> GetAuctionBids(string auctionId)
    {
        return _bids
            .Where(b => b.AuctionId == auctionId && b.IsActive)
            .OrderByDescending(b => b.Amount)
            .ThenBy(b => b.PlacedAt)
            .ToList();
    }

    /// <summary>
    /// Получить мои ставки
    /// </summary>
    public List<Bid> GetUserBids(string userId)
    {
        return _bids.Where(b => b.UserId == userId && b.IsActive).ToList();
    }

    /// <summary>
    /// Получить максимальную ставку на аукцион
    /// </summary>
    public decimal GetMaxBid(string auctionId)
    {
        var maxBid = _bids
            .Where(b => b.AuctionId == auctionId && b.IsActive)
            .MaxBy(b => b.Amount);

        return maxBid?.Amount ?? 0;
    }

    /// <summary>
    /// Делать ставку на аукцион
    /// </summary>
    public (bool success, string message) PlaceBid(string auctionId, string userId, decimal amount)
    {
        // Проверка аукциона
        var auction = _auctionService.GetAuctionById(auctionId);
        if (auction == null)
            return (false, "Аукцион не найден");

        if (auction.Status != AuctionStatus.Active)
            return (false, "Этот аукцион уже завершён");

        if (auction.EndsAt <= DateTime.Now)
            return (false, "Время аукциона истекло");

        // Проверка пользователя
        var user = _userService.GetUserById(userId);
        if (user == null)
            return (false, "Пользователь не найден");

        var isOwnLot = auction.SellerId == userId
            || (!string.IsNullOrWhiteSpace(auction.Seller?.Id) && auction.Seller.Id == userId);
        if (isOwnLot)
            return (false, "Вы не можете делать ставки на свой лот");

        // Проверка возраста
        if (!user.IsAgeVerified)
            return (false, "Вы должны быть старше 18 лет для участия в аукционах");

        // Проверка карты
        if (!user.IsCardLinked)
            return (false, "Вы должны привязать банковскую карту");

        // Найти предыдущую активную ставку этого пользователя (если есть)
        var previousBid = _bids
            .Where(b => b.AuctionId == auctionId && b.UserId == userId && b.IsActive)
            .FirstOrDefault();

        // Проверка доступных средств: если у пользователя уже была ставка, учтём её как доступную (освобождается при перезаписи)
        var effectiveAvailable = user.AvailableBalance + (previousBid?.ReservedAmount ?? 0m);
        if (effectiveAvailable < amount)
            return (false, $"Недостаточно средств. Доступно: {effectiveAvailable} гроши");

        // Проверка минимальной ставки: +10% от текущей максимальной (или стартовой)
        var baseAmount = Math.Max(auction.StartPrice, GetMaxBid(auctionId));
        var minBid = Math.Ceiling(baseAmount * 1.1m);
        if (amount < minBid)
            return (false, $"Ставка должна быть не менее {minBid:N0} грошей");

        // Если текущая максимальная ставка принадлежит другому пользователю — освободим её зарезервированные средства
        var currentHighest = _bids.Where(b => b.AuctionId == auctionId && b.IsActive)
                                 .OrderByDescending(b => b.Amount)
                                 .ThenBy(b => b.PlacedAt)
                                 .FirstOrDefault();

        if (currentHighest != null && currentHighest.UserId != userId)
        {
            var prevUser = _userService.GetUserById(currentHighest.UserId);
            if (prevUser != null)
            {
                _userService.RecordTransaction(prevUser.Id, currentHighest.ReservedAmount, BalanceTransactionType.Release, auctionId, "Возврат средств по перебитой ставке");
                // Просто уменьшаем резерв у предыдущего лидера
                prevUser.ReservedBalance -= currentHighest.ReservedAmount;
                if (prevUser.ReservedBalance < 0) prevUser.ReservedBalance = 0;
            }
            currentHighest.IsActive = false;
        }

        // Отменить предыдущую ставку этого пользователя (если есть) и освободить её зарезервированные средства
        if (previousBid != null)
        {
            previousBid.IsActive = false;
            _userService.RecordTransaction(userId, previousBid.ReservedAmount, BalanceTransactionType.Release, auctionId, "Освобождение средств по предыдущей ставке");
            // Уменьшаем резерв от старой ставки
            user.ReservedBalance -= previousBid.ReservedAmount;
            if (user.ReservedBalance < 0) user.ReservedBalance = 0;
        }

        // Создать новую ставку
        var bid = new Bid
        {
            AuctionId = auctionId,
            UserId = userId,
            Amount = amount,
            ReservedAmount = amount,
            PlacedAt = DateTime.Now,
            IsActive = true,
            User = user
        };

        _bids.Add(bid);

        // Зарезервировать средства (только увеличиваем резерв, баланс остается прежним до покупки)
        user.ReservedBalance += amount;
        _userService.RecordTransaction(userId, amount, BalanceTransactionType.Reserve, auctionId, "Резерв средств под ставку");

        // Обновить аукцион
        auction.CurrentBid = amount;
        auction.CurrentWinnerId = userId;
        auction.BidsCount++;

        // Проверить автоматическое продление
        if (auction.TimeRemaining.TotalMinutes <= auction.AutoExtendMinutes)
        {
            if (auction.ExtensionCount < auction.MaxExtensions)
            {
                auction.EndsAt = auction.EndsAt.AddMinutes(auction.AutoExtendMinutes);
                auction.ExtensionCount++;
            }
        }

        // Уведомить пользователя о ставке: письмо и уведомление
        var subject = "Ваша ставка принята";
        var message = "ваша ставка принята!следите за лотом и не упустите шанс выиграть товар ;)";
        var emailMessage = $"{message}\nЛот: {auction.Title}\nСтавка: {amount:N0} грошей";
        try
        {
            // Используем шаблон template_k2b9lfd для ставок
            var result = _emailService.SendEmailAsync(user.Email, subject, emailMessage, user.FullName, "template_k2b9lfd").GetAwaiter().GetResult();
            _notificationService.CreateSystemNotification(userId, "Ставка принята", message);
        }
        catch
        {
            _notificationService.CreateSystemNotification(userId, "Ставка принята", message);
        }

        _userService.NotifyStateChanged();
        return (true, "Ставка успешно размещена. Проверьте уведомления.");
    }

    /// <summary>
    /// Рассчитать и применить результаты аукциона
    /// </summary>
    public void ProcessAuctionCompletion(string auctionId, User? winner, decimal finalPrice, User? seller = null, string? sellerId = null, decimal sellerProceeds = 0m, string? auctionTitle = null)
    {
        if (winner == null)
            return;

        // Победитель: списываем средства из ОБЩЕГО баланса и освобождаем резерв.
        winner.Balance -= finalPrice;
        winner.ReservedBalance -= finalPrice;
        if (winner.ReservedBalance < 0) winner.ReservedBalance = 0;
        winner.PurchaseCount++;
        _userService.RecordTransaction(winner.Id, finalPrice, BalanceTransactionType.Debit, auctionId, "Покупка лота (списание)");

        seller ??= string.IsNullOrWhiteSpace(sellerId) ? null : _userService.GetUserById(sellerId);
        if (seller != null && sellerProceeds > 0)
        {
            seller.Balance += sellerProceeds;
            seller.SalesCount++;
            _userService.RecordTransaction(seller.Id, sellerProceeds, BalanceTransactionType.Deposit, auctionId, "Доход от продажи лота");
        }

        var otherBids = _bids.Where(b => b.AuctionId == auctionId && b.UserId != winner.Id && b.IsActive).ToList();
        foreach (var bid in otherBids)
        {
            var bidder = _userService.GetUserById(bid.UserId);
            if (bidder != null)
            {
                // Проигравшим просто уменьшаем резерв, так как баланс у них не менялся
                bidder.ReservedBalance -= bid.ReservedAmount;
                if (bidder.ReservedBalance < 0) bidder.ReservedBalance = 0;
            }
            bid.IsActive = false;
        }

        _userService.NotifyStateChanged();
    }

    /// <summary>
    /// Получить все ставки (для персистентности и отладки)
    /// </summary>
    public List<Bid> GetAllBids()
    {
        return _bids;
    }
}
