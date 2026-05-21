using Handal.Client.Models;

namespace Handal.Client.Services;

/// <summary>
/// Сервис для работы с аукционами
/// </summary>
public class AuctionService
{
    private List<Auction> _auctions = new();
    private List<Bid> _bids = new();
    // Внешний провайдер ставок (если BidService зарегистрирован через платформу)
    private BidService? _bidService;

    public void SetBidService(BidService bidService)
    {
        _bidService = bidService;
    }
    private List<AuctionHistory> _history = new();
    private List<ChatMessage> _chatMessages = new();

    /// <summary>
    /// Получить все активные аукционы
    /// </summary>
    public List<Auction> GetActiveAuctions()
    {
        return _auctions.Where(a => a.Status == AuctionStatus.Active).ToList();
    }

    /// <summary>
    /// Получить все аукционы
    /// </summary>
    public List<Auction> GetAllAuctions()
    {
        return _auctions;
    }

    /// <summary>
    /// Получить аукцион по ID
    /// </summary>
    public Auction? GetAuctionById(string id)
    {
        return _auctions.FirstOrDefault(a => a.Id == id);
    }

    /// <summary>
    /// Получить аукционы по категории
    /// </summary>
    public List<Auction> GetAuctionsByCategory(string category)
    {
        return _auctions.Where(a => a.Category == category && a.Status == AuctionStatus.Active).ToList();
    }

    /// <summary>
    /// Получить мои аукционы (проданные)
    /// </summary>
    public List<Auction> GetMyAuctions(string userId)
    {
        return _auctions.Where(a => a.SellerId == userId).ToList();
    }

    /// <summary>
    /// Получить аукционы, на которые я ставил
    /// </summary>
    public List<Auction> GetMyBids(string userId)
    {
        var myBidsAuctionIds = _bids
            .Where(b => b.UserId == userId && b.IsActive)
            .Select(b => b.AuctionId)
            .Distinct();

        return _auctions.Where(a => myBidsAuctionIds.Contains(a.Id)).ToList();
    }

    /// <summary>
    /// Создать новый аукцион
    /// </summary>
    public Auction CreateAuction(
        string sellerId,
        string title,
        string description,
        string category,
        decimal startPrice,
        TimeSpan duration,
        string image,
        bool isFeatured = false,
        bool isCharitable = false,
        decimal charityPercent = 0,
        User? seller = null)
    {
        var auction = new Auction
        {
            SellerId = sellerId,
            Seller = seller,
            Title = title,
            Description = description,
            Category = category,
            StartPrice = startPrice,
            CurrentBid = startPrice,
            Image = image,
            StartedAt = DateTime.Now,
            EndsAt = DateTime.Now.Add(duration),
            IsFeatured = isFeatured,
            IsCharitable = isCharitable,
            CharityPercent = charityPercent,
            Status = AuctionStatus.Pending
        };

        _auctions.Add(auction);
        return auction;
    }

    /// <summary>
    /// Получить лоты на модерации
    /// </summary>
    public List<Auction> GetPendingAuctions()
    {
        return _auctions.Where(a => a.Status == AuctionStatus.Pending).ToList();
    }

    /// <summary>
    /// Оценить лот администрацией и сохранить цену для подтверждения продавцом
    /// </summary>
    public bool AppraiseAuction(string auctionId, decimal appraisedPrice)
    {
        if (appraisedPrice <= 0)
            return false;

        var auction = GetAuctionById(auctionId);
        if (auction == null || auction.Status != AuctionStatus.Pending || auction.IsPriceLockedBySellerDecision)
            return false;

        auction.AppraisedPrice = Math.Ceiling(appraisedPrice);
        auction.AppraisedAt = DateTime.Now;
        auction.SellerAcceptedAppraisal = false;
        auction.SellerRejectedAppraisal = false;
        auction.SellerDecisionAt = null;
        return true;
    }

    /// <summary>
    /// Продавец принимает оценку администратора: цена фиксируется и лот готов к одобрению
    /// </summary>
    public bool AcceptAppraisalBySeller(string auctionId, string sellerId)
    {
        var auction = GetAuctionById(auctionId);
        if (auction == null || auction.Status != AuctionStatus.Pending || auction.SellerId != sellerId)
            return false;
        if (auction.AppraisedPrice is null || auction.IsPriceLockedBySellerDecision)
            return false;

        auction.StartPrice = auction.AppraisedPrice.Value;
        auction.CurrentBid = auction.AppraisedPrice.Value;
        auction.SellerAcceptedAppraisal = true;
        auction.SellerRejectedAppraisal = false;
        auction.SellerDecisionAt = DateTime.Now;
        auction.IsPriceLockedBySellerDecision = true;
        return true;
    }

    /// <summary>
    /// Продавец отказывается от оценки администратора: лот автоматически отклоняется
    /// </summary>
    public bool RejectAppraisalBySeller(string auctionId, string sellerId)
    {
        var auction = GetAuctionById(auctionId);
        if (auction == null || auction.Status != AuctionStatus.Pending || auction.SellerId != sellerId)
            return false;
        if (auction.AppraisedPrice is null || auction.SellerAcceptedAppraisal || auction.SellerRejectedAppraisal)
            return false;

        auction.SellerRejectedAppraisal = true;
        auction.SellerAcceptedAppraisal = false;
        auction.SellerDecisionAt = DateTime.Now;
        return RejectAuction(auctionId);
    }

    /// <summary>
    /// Одобрить лот
    /// </summary>
    public bool ApproveAuction(string auctionId)
    {
        var auction = GetAuctionById(auctionId);
        if (auction == null || auction.Status != AuctionStatus.Pending)
            return false;

        var plannedDuration = auction.EndsAt - auction.StartedAt;
        if (plannedDuration <= TimeSpan.Zero)
            plannedDuration = TimeSpan.FromHours(6);

        auction.Status = AuctionStatus.Active;
        auction.StartedAt = DateTime.Now; // Начинаем отсчет времени с момента одобрения
        auction.EndsAt = auction.StartedAt.Add(plannedDuration); // Обновляем дедлайн относительно одобрения
        return true;
    }

    /// <summary>
    /// Отклонить лот
    /// </summary>
    public bool RejectAuction(string auctionId)
    {
        var auction = GetAuctionById(auctionId);
        if (auction == null || auction.Status != AuctionStatus.Pending)
            return false;

        auction.Status = AuctionStatus.Cancelled;
        auction.FinalPrice = null;

        if (_history.All(h => h.AuctionId != auctionId))
        {
            var history = new AuctionHistory
            {
                AuctionId = auctionId,
                Auction = auction,
                SellerId = auction.SellerId,
                WinnerId = null,
                FinalPrice = 0,
                SellerProceeds = 0,
                CharityAmount = 0,
                CompletedAt = DateTime.Now,
                FinalStatus = auction.Status,
                ParticipantsCount = 0,
                BidsCount = auction.BidsCount,
                ViewsCount = auction.ViewsCount
            };

            _history.Add(history);
        }

        return true;
    }

    /// <summary>
    /// Досрочно завершить аукцион продавцом (удаление из ленты)
    /// </summary>
    public bool EndAuctionBySeller(string auctionId, string sellerId)
    {
        var result = EndAuctionBySellerWithResult(auctionId, sellerId);
        return result.Success;
    }

    /// <summary>
    /// Досрочно завершить аукцион продавцом с детальным результатом (победитель/проигравшие/история)
    /// </summary>
    public AuctionSellerEndResult EndAuctionBySellerWithResult(string auctionId, string sellerId)
    {
        var auction = GetAuctionById(auctionId);
        if (auction == null || auction.SellerId != sellerId || auction.Status != AuctionStatus.Active)
            return new AuctionSellerEndResult { Success = false };

        var bidsSource = _bidService?.GetAuctionBids(auctionId)
            ?? _bids.Where(b => b.AuctionId == auctionId && b.IsActive).ToList();
        var winningBid = bidsSource
            .OrderByDescending(b => b.Amount)
            .ThenBy(b => b.PlacedAt)
            .FirstOrDefault();
        var losingBidderIds = bidsSource
            .Where(b => winningBid == null || b.UserId != winningBid.UserId)
            .Select(b => b.UserId)
            .Distinct()
            .ToList();

        AuctionHistory history;
        if (winningBid == null)
        {
            // Если ставок нет — это отмена продавцом
            _bidService?.CancelAuctionBids(auctionId);
            auction.Status = AuctionStatus.Cancelled;
            auction.FinalPrice = null;

            history = new AuctionHistory
            {
                AuctionId = auctionId,
                Auction = auction,
                SellerId = auction.SellerId,
                WinnerId = null,
                FinalPrice = 0,
                SellerProceeds = 0,
                CharityAmount = 0,
                FinalStatus = auction.Status,
                ParticipantsCount = 0,
                BidsCount = auction.BidsCount,
                ViewsCount = auction.ViewsCount
            };
            _history.Add(history);
            return new AuctionSellerEndResult
            {
                Success = true,
                Auction = auction,
                History = history,
                WinnerBid = null,
                LosingBidderIds = new List<string>()
            };
        }

        // Если ставки есть — фиксируем победителя даже при досрочном завершении продавцом
        auction.Status = AuctionStatus.Completed;
        auction.CurrentWinnerId = winningBid.UserId;
        auction.FinalPrice = winningBid.Amount;
        winningBid.IsWinning = true;

        history = new AuctionHistory
        {
            AuctionId = auctionId,
            Auction = auction,
            SellerId = auction.SellerId,
            WinnerId = winningBid.UserId,
            Winner = winningBid.User,
            FinalPrice = winningBid.Amount,
            SellerProceeds = CalculateSellerProceeds(auction),
            CharityAmount = CalculateCharityAmount(auction),
            CompletedAt = DateTime.Now,
            FinalStatus = auction.Status,
            ParticipantsCount = bidsSource.Select(b => b.UserId).Distinct().Count(),
            ParticipantIds = bidsSource.Select(b => b.UserId).Distinct().ToList(),
            BidsCount = auction.BidsCount,
            ViewsCount = auction.ViewsCount
        };

        // Провести финансовые списания/зачисления и освободить резервы остальных
        if (_bidService != null)
        {
            var winnerUser = winningBid.User ?? _bidService.ResolveUser(winningBid.UserId);
            var sellerUser = auction.Seller ?? _bidService.ResolveUser(auction.SellerId);
            _bidService.ProcessAuctionCompletion(
                auctionId,
                winnerUser,
                winningBid.Amount,
                sellerUser,
                auction.SellerId,
                history.SellerProceeds,
                auction.Title);
        }

        _history.Add(history);
        return new AuctionSellerEndResult
        {
            Success = true,
            Auction = auction,
            History = history,
            WinnerBid = winningBid,
            LosingBidderIds = losingBidderIds
        };
    }

    /// <summary>
    /// Получить историю аукциона
    /// </summary>
    public AuctionHistory? GetAuctionHistory(string auctionId)
    {
        return _history.FirstOrDefault(h => h.AuctionId == auctionId);
    }

    /// <summary>
    /// Получить историю по продавцу
    /// </summary>
    public List<AuctionHistory> GetHistoryBySeller(string sellerId)
    {
        EnsureHistoryUpToDate();
        return _history.Where(h => h.SellerId == sellerId).ToList();
    }

    /// <summary>
    /// Получить историю по покупателю
    /// </summary>
    public List<AuctionHistory> GetHistoryByBuyer(string buyerId)
    {
        EnsureHistoryUpToDate();
        return _history.Where(h => h.WinnerId == buyerId).ToList();
    }

    /// <summary>
    /// Получить историю по участнику (все завершенные аукционы, где пользователь делал ставки)
    /// </summary>
    public List<AuctionHistory> GetHistoryByParticipant(string userId)
    {
        EnsureHistoryUpToDate();
        return _history.Where(h => h.ParticipantIds.Contains(userId)).ToList();
    }

    /// <summary>
    /// Завершить аукцион (вызывается по таймеру)
    /// </summary>
    public AuctionHistory? CompleteAuction(string auctionId)
    {
        var auction = GetAuctionById(auctionId);
        if (auction == null || auction.Status != AuctionStatus.Active)
            return null;

        // Определить победителя: сначала пытаемся получить ставки из внешнего BidService, иначе используем внутренний список
        List<Bid> bidsSource;
        if (_bidService != null)
        {
            bidsSource = _bidService.GetAuctionBids(auctionId);
        }
        else
        {
            bidsSource = _bids.Where(b => b.AuctionId == auctionId && b.IsActive).ToList();
        }

        var winningBid = bidsSource
            .OrderByDescending(b => b.Amount)
            .ThenBy(b => b.PlacedAt)
            .FirstOrDefault();
        var seller = auction.Seller;

        if (winningBid == null)
        {
            // Аукцион не состоялся (нет ставок)
            auction.Status = AuctionStatus.NotHeld;
            auction.FinalPrice = null;
        }
        else
        {
            // Установить победителя и финальную цену
            auction.Status = AuctionStatus.Completed;
            auction.CurrentWinnerId = winningBid.UserId;
            auction.FinalPrice = winningBid.Amount;
            winningBid.IsWinning = true;
        }

        // Создать запись в историю
        var history = new AuctionHistory
        {
            AuctionId = auctionId,
            Auction = auction,
            SellerId = auction.SellerId,
            WinnerId = auction.CurrentWinnerId,
            Winner = winningBid?.User,
            FinalPrice = auction.FinalPrice ?? 0,
            SellerProceeds = CalculateSellerProceeds(auction),
            CharityAmount = CalculateCharityAmount(auction),
            CompletedAt = DateTime.Now,
            FinalStatus = auction.Status,
            ParticipantsCount = bidsSource.Select(b => b.UserId).Distinct().Count(),
            ParticipantIds = bidsSource.Select(b => b.UserId).Distinct().ToList(),
            BidsCount = auction.BidsCount,
            ViewsCount = auction.ViewsCount
        };

        if (winningBid != null && _bidService != null)
        {
            var winnerUser = winningBid.User ?? _bidService.ResolveUser(winningBid.UserId);
            var sellerUser = seller ?? _bidService.ResolveUser(auction.SellerId);
            _bidService.ProcessAuctionCompletion(auctionId, winnerUser, winningBid.Amount, sellerUser, auction.SellerId, history.SellerProceeds, auction.Title);
        }

        _history.Add(history);
        return history;
    }

    /// <summary>
    /// Проверить и завершить истекшие аукционы
    /// </summary>
    public List<AuctionHistory> CompleteExpiredAuctions()
    {
        var completed = new List<AuctionHistory>();
        var activeAuctions = GetActiveAuctions();

        foreach (var auction in activeAuctions)
        {
            if (auction.EndsAt <= DateTime.Now)
            {
                var history = CompleteAuction(auction.Id);
                if (history != null)
                    completed.Add(history);
            }
        }

        return completed;
    }

    /// <summary>
    /// Посчитать деньги продавца (за вычетом благотворительности)
    /// </summary>
    private decimal CalculateCharityAmount(Auction auction)
    {
        if (!auction.IsCharitable || auction.FinalPrice == null)
            return 0;

        return (auction.FinalPrice.Value * (auction.CharityPercent / 100m));
    }

    private decimal CalculateSellerProceeds(Auction auction)
    {
        if (auction.FinalPrice == null || auction.FinalPrice == 0)
            return 0;

        if (auction.IsCharitable)
        {
            var charityAmount = auction.FinalPrice.Value * (auction.CharityPercent / 100m);
            return auction.FinalPrice.Value - charityAmount;
        }

        return auction.FinalPrice.Value;
    }

    /// <summary>
    /// Получить всю историю (для персистентности и отладки)
    /// </summary>
    public List<AuctionHistory> GetAllHistory()
    {
        EnsureHistoryUpToDate();
        return _history;
    }

    private void EnsureHistoryUpToDate()
    {
        CompleteExpiredAuctions();
    }

    /// <summary>
    /// Получить все сообщения чата (для персистентности)
    /// </summary>
    public List<ChatMessage> GetAllChatMessages()
    {
        return _chatMessages;
    }
}

public class AuctionSellerEndResult
{
    public bool Success { get; set; }
    public Auction? Auction { get; set; }
    public AuctionHistory? History { get; set; }
    public Bid? WinnerBid { get; set; }
    public List<string> LosingBidderIds { get; set; } = new();
}
