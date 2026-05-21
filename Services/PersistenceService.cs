using System.Reflection;
using System.Text.Json;
using Handal.Client.Models;
using Microsoft.JSInterop;

namespace Handal.Client.Services;

public class PersistenceService
{
    private readonly string _dataDir;
    private readonly IJSInProcessRuntime? _js;
    private const string UsersKey = "users";
    private const string AuctionsKey = "auctions";
    private const string BidsKey = "bids";
    private const string HistoryKey = "auction_history";
    private const string ChatKey = "chat_messages";
    private const string NotificationsKey = "notifications";
    private const string VerificationCodesKey = "verification_codes";
    private const string BalanceTransactionsKey = "balance_transactions";
    private const string CurrentUserKey = "current_user_id";
    private const string TagsKey = "tags";
    private const string TagAssignmentsKey = "tag_assignments";

    public PersistenceService(string? basePath = null)
    {
        _dataDir = Path.Combine(basePath ?? Directory.GetCurrentDirectory(), "data");
        Directory.CreateDirectory(_dataDir);
    }

    public PersistenceService(IJSRuntime js, string? basePath = null)
        : this(basePath)
    {
        _js = js as IJSInProcessRuntime;
    }

    public void Save(AuctionPlatformService platform)
    {
        var opts = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        var auctionsToStore = platform.AuctionService
            .GetAllAuctions()
            .Select(ToPersistedAuction)
            .ToList();
        void SavePart<T>(string key, string filename, T data)
        {
            var json = JsonSerializer.Serialize(data, opts);
            if (_js is not null)
            {
                try { _js.InvokeVoid("localStorage.setItem", key, json); }
                catch { /* ignore */ }
            }
            try
            {
                File.WriteAllText(Path.Combine(_dataDir, filename), json);
            }
            catch
            {
            }
        }
        SavePart(UsersKey, "users.json", platform.UserService.GetAllUsers());
        SavePart(AuctionsKey, "auctions.json", auctionsToStore);
        SavePart(BidsKey, "bids.json", platform.BidService.GetAllBids());
        SavePart(HistoryKey, "history.json", platform.AuctionService.GetAllHistory());
        SavePart(ChatKey, "chat_messages.json", platform.ChatService.GetAllMessages());
        SavePart(NotificationsKey, "notifications.json", platform.NotificationService.GetAllNotifications());
        SavePart(VerificationCodesKey, "verification_codes.json", platform.UserService.GetAllVerificationCodes());
        SavePart(BalanceTransactionsKey, "balance_transactions.json", platform.UserService.GetAllBalanceTransactions());
        SavePart(CurrentUserKey, "current_user.json", platform.UserService.CurrentUser?.Id ?? "");
        SavePart(TagsKey, "tags.json", platform.TagService.GetAllTagDefinitions());
        SavePart(TagAssignmentsKey, "tag_assignments.json", platform.TagService.GetAllAssignments());
    }

    private static PersistedAuction ToPersistedAuction(Auction source)
    {
        return new PersistedAuction
        {
            Id = source.Id,
            Title = source.Title,
            Description = source.Description,
            Category = source.Category,
            SellerId = source.SellerId,
            StartPrice = source.StartPrice,
            CurrentBid = source.CurrentBid,
            CurrentWinnerId = source.CurrentWinnerId,
            FinalPrice = source.FinalPrice,
            BidsCount = source.BidsCount,
            ViewsCount = source.ViewsCount,
            Image = source.Image,
            Images = source.Images.ToList(),
            StartedAt = source.StartedAt,
            EndsAt = source.EndsAt,
            Status = source.Status,
            IsFeatured = source.IsFeatured,
            IsCharitable = source.IsCharitable,
            CharityPercent = source.CharityPercent,
            ExtensionCount = source.ExtensionCount,
            MaxExtensions = source.MaxExtensions,
            AutoExtendMinutes = source.AutoExtendMinutes,
            AppraisedPrice = source.AppraisedPrice,
            AppraisedAt = source.AppraisedAt,
            SellerAcceptedAppraisal = source.SellerAcceptedAppraisal,
            SellerRejectedAppraisal = source.SellerRejectedAppraisal,
            SellerDecisionAt = source.SellerDecisionAt,
            IsPriceLockedBySellerDecision = source.IsPriceLockedBySellerDecision
        };
    }

    private sealed class PersistedAuction
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string SellerId { get; set; } = string.Empty;
        public decimal StartPrice { get; set; }
        public decimal CurrentBid { get; set; }
        public string? CurrentWinnerId { get; set; }
        public decimal? FinalPrice { get; set; }
        public int BidsCount { get; set; }
        public int ViewsCount { get; set; }
        public string Image { get; set; } = string.Empty;
        public List<string> Images { get; set; } = new();
        public DateTime StartedAt { get; set; }
        public DateTime EndsAt { get; set; }
        public AuctionStatus Status { get; set; }
        public bool IsFeatured { get; set; }
        public bool IsCharitable { get; set; }
        public decimal CharityPercent { get; set; }
        public int ExtensionCount { get; set; }
        public int MaxExtensions { get; set; }
        public int AutoExtendMinutes { get; set; }
        public decimal? AppraisedPrice { get; set; }
        public DateTime? AppraisedAt { get; set; }
        public bool SellerAcceptedAppraisal { get; set; }
        public bool SellerRejectedAppraisal { get; set; }
        public DateTime? SellerDecisionAt { get; set; }
        public bool IsPriceLockedBySellerDecision { get; set; }
    }

    public bool Load(AuctionPlatformService platform)
    {
        var hasData = false;
        string? LoadPart(string key, string filename)
        {
            string? j = null;
            if (_js is not null)
            {
                try { j = _js.Invoke<string>("localStorage.getItem", key); }
                catch { j = null; }
            }
            if (!string.IsNullOrEmpty(j))
            {
                hasData = true;
            }
            if (string.IsNullOrEmpty(j))
            {
                var path = Path.Combine(_dataDir, filename);
                if (File.Exists(path))
                {
                    j = File.ReadAllText(path);
                    if (!string.IsNullOrEmpty(j))
                        hasData = true;
                }
            }
            return j;
        }
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // Заполнить приватные поля через reflection
        var usersField = platform.UserService.GetType().GetField("_users", BindingFlags.NonPublic | BindingFlags.Instance);
        if (usersField != null)
        {
            var usersList = usersField.GetValue(platform.UserService) as List<User>;
            usersList?.Clear();
            var usersJson = LoadPart(UsersKey, "users.json");
            var users = string.IsNullOrEmpty(usersJson) ? null : JsonSerializer.Deserialize<List<User>>(usersJson, opts);
            if (users != null) usersList?.AddRange(users);
        }
        // Восстановить текущего пользователя
        var currentJson = LoadPart(CurrentUserKey, "current_user.json");
        string? currentUserMarker = null;
        if (!string.IsNullOrWhiteSpace(currentJson))
        {
            try
            {
                currentUserMarker = JsonSerializer.Deserialize<string>(currentJson, opts);
            }
            catch
            {
                currentUserMarker = currentJson.Trim().Trim('"');
            }
        }
        if (!string.IsNullOrWhiteSpace(currentUserMarker))
        {
            var usersForLookupField = platform.UserService.GetType().GetField("_users", BindingFlags.NonPublic | BindingFlags.Instance);
            var usersForLookup = usersForLookupField?.GetValue(platform.UserService) as List<User>;
            var current = usersForLookup?.FirstOrDefault(u => u.Id == currentUserMarker)
                ?? usersForLookup?.FirstOrDefault(u => u.Email == currentUserMarker);
            if (current != null)
            {
                var prop = platform.UserService.GetType().GetProperty("CurrentUser",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop != null && prop.CanWrite)
                {
                    prop.SetValue(platform.UserService, current);
                }
                else
                {
                    var backing = platform.UserService.GetType().GetField("<CurrentUser>k__BackingField",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    backing?.SetValue(platform.UserService, current);
                }
            }
        }

        var auctionsField = platform.AuctionService.GetType().GetField("_auctions", BindingFlags.NonPublic | BindingFlags.Instance);
        if (auctionsField != null)
        {
            var list = auctionsField.GetValue(platform.AuctionService) as List<Auction>;
            list?.Clear();
            var auctionsJson = LoadPart(AuctionsKey, "auctions.json");
            var auctions = string.IsNullOrEmpty(auctionsJson) ? null : JsonSerializer.Deserialize<List<Auction>>(auctionsJson, opts);
            if (auctions != null) list?.AddRange(auctions);
        }

        var bidsField = platform.BidService.GetType().GetField("_bids", BindingFlags.NonPublic | BindingFlags.Instance);
        if (bidsField != null)
        {
            var list = bidsField.GetValue(platform.BidService) as List<Bid>;
            list?.Clear();
            var bidsJson = LoadPart(BidsKey, "bids.json");
            var bids = string.IsNullOrEmpty(bidsJson) ? null : JsonSerializer.Deserialize<List<Bid>>(bidsJson, opts);
            if (bids != null) list?.AddRange(bids);
        }

        var historyField = platform.AuctionService.GetType().GetField("_history", BindingFlags.NonPublic | BindingFlags.Instance);
        if (historyField != null)
        {
            var list = historyField.GetValue(platform.AuctionService) as List<AuctionHistory>;
            list?.Clear();
            var historyJson = LoadPart(HistoryKey, "history.json");
            var history = string.IsNullOrEmpty(historyJson) ? null : JsonSerializer.Deserialize<List<AuctionHistory>>(historyJson, opts);
            if (history != null) list?.AddRange(history);
        }

        var chatField = platform.ChatService.GetType().GetField("_messages", BindingFlags.NonPublic | BindingFlags.Instance);
        if (chatField != null)
        {
            var list = chatField.GetValue(platform.ChatService) as List<ChatMessage>;
            list?.Clear();
            var chatJson = LoadPart(ChatKey, "chat_messages.json");
            var chats = string.IsNullOrEmpty(chatJson) ? null : JsonSerializer.Deserialize<List<ChatMessage>>(chatJson, opts);
            if (chats != null) list?.AddRange(chats);
        }

        // Уведомления
        var notifField = platform.NotificationService.GetType().GetField("_notifications", BindingFlags.NonPublic | BindingFlags.Instance);
        if (notifField != null)
        {
            var list = notifField.GetValue(platform.NotificationService) as List<Notification>;
            list?.Clear();
            var notifJson = LoadPart(NotificationsKey, "notifications.json");
            var notifs = string.IsNullOrEmpty(notifJson) ? null : JsonSerializer.Deserialize<List<Notification>>(notifJson, opts);
            if (notifs != null) list?.AddRange(notifs);
        }
        var tagsField = platform.TagService.GetType().GetField("_tagDefinitions", BindingFlags.NonPublic | BindingFlags.Instance);
        if (tagsField != null)
        {
            var list = tagsField.GetValue(platform.TagService) as List<TagDefinition>;
            list?.Clear();
            var tagsJson = LoadPart(TagsKey, "tags.json");
            var tags = string.IsNullOrEmpty(tagsJson) ? null : JsonSerializer.Deserialize<List<TagDefinition>>(tagsJson, opts);
            if (tags != null) list?.AddRange(tags);
        }
        var tagAssignField = platform.TagService.GetType().GetField("_assignments", BindingFlags.NonPublic | BindingFlags.Instance);
        if (tagAssignField != null)
        {
            var list = tagAssignField.GetValue(platform.TagService) as List<AuctionTagAssignment>;
            list?.Clear();
            var asgJson = LoadPart(TagAssignmentsKey, "tag_assignments.json");
            var asgs = string.IsNullOrEmpty(asgJson) ? null : JsonSerializer.Deserialize<List<AuctionTagAssignment>>(asgJson, opts);
            if (asgs != null) list?.AddRange(asgs);
        }

        // verification codes
        var verifField = platform.UserService.GetType().GetField("_verificationCodes", BindingFlags.NonPublic | BindingFlags.Instance);
        if (verifField != null)
        {
            var list = verifField.GetValue(platform.UserService) as List<VerificationCodeEntry>;
            list?.Clear();
            var verifJson = LoadPart(VerificationCodesKey, "verification_codes.json");
            var codes = string.IsNullOrEmpty(verifJson) ? null : JsonSerializer.Deserialize<List<VerificationCodeEntry>>(verifJson, opts);
            if (codes != null) list?.AddRange(codes);
        }
        // balance transactions
        var balField = platform.UserService.GetType().GetField("_balanceTransactions", BindingFlags.NonPublic | BindingFlags.Instance);
        if (balField != null)
        {
            var list = balField.GetValue(platform.UserService) as List<BalanceTransaction>;
            list?.Clear();
            var balJson = LoadPart(BalanceTransactionsKey, "balance_transactions.json");
            var txs = string.IsNullOrEmpty(balJson) ? null : JsonSerializer.Deserialize<List<BalanceTransaction>>(balJson, opts);
            if (txs != null) list?.AddRange(txs);
        }

        return hasData;
    }
}

// Legacy class removed in favor of split files
