using Handal.Client.Models;

namespace Handal.Client.Services;

public class TagService
{
    private readonly NotificationService _notificationService;
    private readonly AuctionService _auctionService;
    private readonly UserService _userService;
    private readonly List<TagDefinition> _tagDefinitions = new();
    private readonly List<AuctionTagAssignment> _assignments = new();
    private readonly Dictionary<string, int> _tagQueryCounter = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _userLastTagCreation = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _userDailyNewTagCounter = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _blockedWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "черный список", "ругательство", "запрещено", "xxx"
    };
    private readonly Dictionary<string, string> _synonyms = new(StringComparer.OrdinalIgnoreCase)
    {
        { "авто", "автомобиль" },
        { "машина", "автомобиль" },
        { "часы наручные", "часы" },
        { "монеты", "монета" },
        { "книжица", "книга" }
    };

    private readonly List<TagModerationLogEntry> _moderationLogs = new();

    private const int MaxChildTagsPerAuction = 10;
    private const int DailyNewTagsLimitPerUser = 5;

    private static readonly string[] SystemCategories = new[]
    {
        "Часы", "Антиквариат", "Книги", "Искусство", "Украшения", "Этнос", "Мебель", "История", "Музыка", "Оружие"
    };

    public TagService(NotificationService notificationService, AuctionService auctionService, UserService userService)
    {
        _notificationService = notificationService;
        _auctionService = auctionService;
        _userService = userService;
        SeedApprovedWhitelist();
    }

    private void SeedApprovedWhitelist()
    {
        if (_tagDefinitions.Count > 0) return;
        var seed = new (string name, string category)[]
        {
            ("винтаж", "Антиквариат"),
            ("антиквариат", "Антиквариат"),
            ("редкость", "Антиквариат"),
            ("коллекционный", "Антиквариат"),
            ("исторический", "История"),
            ("ручная работа", "Искусство"),
            ("бронза", "Искусство"),
            ("масло", "Искусство"),
            ("картина", "Искусство"),
            ("портрет", "Искусство"),
            ("мозаика", "Искусство"),
            ("статуэтка", "Искусство"),
            ("фарфор", "Этнос"),
            ("керамика", "Этнос"),
            ("ваза", "Этнос"),
            ("серебро", "Антиквариат"),
            ("золото", "Украшения"),
            ("бриллиант", "Украшения"),
            ("сапфир", "Украшения"),
            ("кулон", "Украшения"),
            ("кольцо", "Украшения"),
            ("механические", "Часы"),
            ("швейцарские", "Часы"),
            ("винтажные часы", "Часы"),
            ("rolex", "Часы"),
            ("будильник", "Часы"),
            ("музыка", "Музыка"),
            ("опера", "Музыка"),
            ("икона", "История"),
            ("орден", "История"),
            ("монета", "История"),
            ("марка", "История"),
            ("компас", "История"),
            ("граммофон", "Музыка"),
            ("винил", "Музыка"),
            ("мейсен", "Этнос"),
            ("самовар", "Мебель"),
            ("мебель", "Мебель"),
            ("люстра", "Мебель"),
            ("каподимонте", "Искусство"),
            ("автомобиль", "Антиквариат"),
            ("книга", "Книги"),
            ("редкое издание", "Книги"),
            ("оружие", "Оружие"),
            ("шпага", "Оружие")
        };
        foreach (var s in seed)
        {
            var n = Normalize(MapSynonym(s.name));
            if (_tagDefinitions.Any(t => t.NormalizedName == n)) continue;
            _tagDefinitions.Add(new TagDefinition
            {
                Name = s.name,
                NormalizedName = n,
                Category = SystemCategories.Contains(s.category) ? s.category : "Антиквариат",
                Status = ModerationStatus.Approved,
                CreatedByUserId = "system",
                CreatedAt = DateTime.Now.AddDays(-7),
                ModeratedByAdminId = "admin-internal",
                ModeratedAt = DateTime.Now.AddDays(-7),
                UsageCount = 0
            });
        }
    }

    public List<string> GetSystemCategories() => SystemCategories.ToList();
    public List<TagDefinition> GetAllTagDefinitions() => _tagDefinitions;
    public List<AuctionTagAssignment> GetAllAssignments() => _assignments;
    public List<AuctionTagAssignment> GetAssignmentsForAuction(string auctionId) => _assignments.Where(x => x.AuctionId == auctionId).ToList();
    public List<TagDefinition> SearchDefinitions(string? query, ModerationStatus? status, string? category)
    {
        var q = _tagDefinitions.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var l = query.Trim().ToLowerInvariant();
            q = q.Where(t => t.Name.ToLowerInvariant().Contains(l));
        }
        if (status.HasValue) q = q.Where(t => t.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(category)) q = q.Where(t => string.Equals(t.Category, category, StringComparison.OrdinalIgnoreCase));
        return q.OrderBy(t => t.Name).ToList();
    }
    public List<AuctionTagAssignment> SearchAssignments(string? query, ModerationStatus? status, string? category)
    {
        var q = _assignments.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var l = query.Trim().ToLowerInvariant();
            q = q.Where(a =>
            {
                var tag = _tagDefinitions.FirstOrDefault(t => t.Id == a.TagDefinitionId);
                return a.RawInput.ToLowerInvariant().Contains(l) || (tag != null && tag.Name.ToLowerInvariant().Contains(l));
            });
        }
        if (status.HasValue) q = q.Where(a => a.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(category)) q = q.Where(a => string.Equals(a.Category, category, StringComparison.OrdinalIgnoreCase));
        return q.OrderByDescending(a => a.CreatedAt).ToList();
    }

    public TagDefinition? FindDefinitionByNormalized(string normalized) =>
        _tagDefinitions.FirstOrDefault(t => t.NormalizedName == normalized);
    public bool HasApprovedDefinition(string input)
    {
        var normalized = Normalize(MapSynonym(input));
        return _tagDefinitions.Any(t => t.NormalizedName == normalized && t.Status == ModerationStatus.Approved);
    }

    public static string Normalize(string name)
    {
        var raw = (name ?? string.Empty).ToLowerInvariant();
        var builder = new System.Text.StringBuilder(raw.Length);
        foreach (var c in raw)
        {
            if (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))
            {
                builder.Append(c);
            }
            else
            {
                builder.Append(' ');
            }
        }

        var normalized = builder.ToString().Trim();
        while (normalized.Contains("  "))
            normalized = normalized.Replace("  ", " ");
        return normalized;
    }

    private string MapSynonym(string raw)
    {
        var norm = Normalize(raw);
        if (_synonyms.TryGetValue(norm, out var canonical))
            return canonical;
        return raw;
    }

    public string CanonicalizeTagName(string raw) => MapSynonym(raw).Trim();

    private bool IsAllowedTagName(string raw)
    {
        var n = Normalize(raw);
        if (n.Length < 2) return false;
        if (_blockedWords.Any(b => n.Contains(b))) return false;
        return true;
    }

    public string? ValidateTagDraft(string userId, string rawTag, IEnumerable<string> currentDraftTags)
    {
        if (string.IsNullOrWhiteSpace(rawTag))
            return "Тег пуст";

        var canonical = CanonicalizeTagName(rawTag);
        if (!IsAllowedTagName(canonical))
            return "Недопустимый тег";

        var normalized = Normalize(canonical);
        if (currentDraftTags.Any(t => Normalize(MapSynonym(t)) == normalized))
            return "Тег уже добавлен";

        var existing = FindDefinitionByNormalized(normalized);
        if (existing != null && existing.Status == ModerationStatus.Rejected)
            return "Тег отклонён модератором";

        if (existing == null && !CanCreateNewTag(userId))
            return "Достигнут дневной лимит на новые теги";

        return null;
    }

    private bool CanCreateNewTag(string userId)
    {
        var todayKey = $"{userId}:{DateTime.Today:yyyyMMdd}";
        if (!_userDailyNewTagCounter.TryGetValue(todayKey, out var count))
            return true;
        return count < DailyNewTagsLimitPerUser;
    }

    private void IncrementUserNewTag(string userId)
    {
        var todayKey = $"{userId}:{DateTime.Today:yyyyMMdd}";
        _userDailyNewTagCounter.TryGetValue(todayKey, out var count);
        _userDailyNewTagCounter[todayKey] = count + 1;
        _userLastTagCreation[userId] = DateTime.Now;
    }

    public List<string> GetAutocompleteSuggestions(string input, int limit = 8, int minUsage = 1)
    {
        var q = Normalize(MapSynonym(input));
        if (string.IsNullOrWhiteSpace(q)) return new();
        return _tagDefinitions
            .Where(t => t.Status == ModerationStatus.Approved && t.UsageCount >= minUsage && IsAutocompleteMatch(t, q))
            .OrderByDescending(t => t.UsageCount)
            .ThenBy(t => t.Name)
            .Take(limit)
            .Select(t => t.Name)
            .ToList();
    }

    private static bool IsAutocompleteMatch(TagDefinition tag, string query)
    {
        var name = tag.Name.ToLowerInvariant();
        var normalizedName = tag.NormalizedName;

        return name.StartsWith(query)
            || normalizedName.StartsWith(query)
            || name.Contains(" " + query)
            || normalizedName.Contains(" " + query);
    }

    public List<TagDefinition> GetTrendingTags(int minUsage = 3, int top = 20)
    {
        return _tagDefinitions
            .Where(t => t.Status == ModerationStatus.Approved && t.UsageCount >= minUsage)
            .OrderByDescending(t => t.UsageCount)
            .ThenBy(t => t.Name)
            .Take(top)
            .ToList();
    }

    public void EnsureSuggestedTagsForExistingAuctions()
    {
        foreach (var auction in _auctionService.GetAllAuctions())
        {
            foreach (var tagName in GetAutoTagCandidates(auction))
            {
                var normalized = Normalize(MapSynonym(tagName));
                var definition = _tagDefinitions.FirstOrDefault(t =>
                    t.NormalizedName == normalized && t.Status == ModerationStatus.Approved);
                if (definition == null)
                    continue;

                var exists = _assignments.Any(a =>
                    a.AuctionId == auction.Id &&
                    a.TagDefinitionId == definition.Id &&
                    a.Status == ModerationStatus.Approved);

                if (exists)
                    continue;

                _assignments.Add(new AuctionTagAssignment
                {
                    AuctionId = auction.Id,
                    TagDefinitionId = definition.Id,
                    RawInput = definition.Name,
                    NormalizedTag = definition.NormalizedName,
                    Category = definition.Category,
                    Status = ModerationStatus.Approved,
                    CreatedByUserId = "system",
                    CreatedAt = DateTime.Now.AddDays(-3),
                    ModeratedByAdminId = "admin-internal",
                    ModeratedAt = DateTime.Now.AddDays(-3)
                });
                definition.UsageCount++;
            }
        }
    }

    private List<string> GetAutoTagCandidates(Auction auction)
    {
        var source = $"{auction.Title} {auction.Description} {auction.Category}".ToLowerInvariant();
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in _tagDefinitions.Where(t => t.Status == ModerationStatus.Approved))
        {
            if (source.Contains(definition.Name.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))
                tags.Add(definition.Name);
        }

        var keywordMap = new Dictionary<string, string[]>
        {
            ["Часы"] = new[] { "механические", "винтажные часы" },
            ["Антиквариат"] = new[] { "антиквариат", "коллекционный" },
            ["Книги"] = new[] { "книга", "редкое издание" },
            ["Искусство"] = new[] { "картина", "ручная работа" },
            ["Украшения"] = new[] { "золото", "бриллиант" },
            ["Этнос"] = new[] { "фарфор", "керамика" },
            ["Мебель"] = new[] { "мебель", "люстра" },
            ["История"] = new[] { "исторический", "монета" },
            ["Музыка"] = new[] { "музыка", "винил" },
            ["Оружие"] = new[] { "оружие", "шпага" }
        };

        if (keywordMap.TryGetValue(auction.Category, out var categoryTags))
        {
            foreach (var categoryTag in categoryTags)
                tags.Add(categoryTag);
        }

        return tags.Take(6).ToList();
    }

    public AuctionTagAttachResult AttachTagToAuction(string userId, string auctionId, string rawTag, string? sellerSelectedCategory)
    {
        var auction = _auctionService.GetAuctionById(auctionId);
        if (auction == null) return new AuctionTagAttachResult { Success = false, Error = "Аукцион не найден" };
        if (string.IsNullOrWhiteSpace(rawTag)) return new AuctionTagAttachResult { Success = false, Error = "Тег пуст" };
        if (!IsAllowedTagName(rawTag)) return new AuctionTagAttachResult { Success = false, Error = "Недопустимый тег" };
        var auctionTagsCount = _assignments.Count(a => a.AuctionId == auctionId && a.Status != ModerationStatus.Rejected);
        if (auctionTagsCount >= MaxChildTagsPerAuction) return new AuctionTagAttachResult { Success = false, Error = "Достигнут лимит тегов для лота (10)" };
        var normalized = Normalize(MapSynonym(rawTag));
        if (_assignments.Any(a => a.AuctionId == auctionId && a.NormalizedTag == normalized && a.Status != ModerationStatus.Rejected))
            return new AuctionTagAttachResult { Success = false, Error = "Тег уже добавлен к лоту" };
        var existing = FindDefinitionByNormalized(normalized);
        if (existing != null && existing.Status == ModerationStatus.Rejected)
            return new AuctionTagAttachResult { Success = false, Error = "Тег отклонён модератором" };

        TagDefinition definition;
        var createdNew = false;
        if (existing == null)
        {
            if (!CanCreateNewTag(userId))
                return new AuctionTagAttachResult { Success = false, Error = "Достигнут дневной лимит на новые теги" };
            var category = string.IsNullOrWhiteSpace(sellerSelectedCategory) ? auction.Category : sellerSelectedCategory!;
            if (!SystemCategories.Contains(category)) category = "Антиквариат";
            definition = new TagDefinition
            {
                Name = MapSynonym(rawTag).Trim(),
                NormalizedName = normalized,
                Category = category,
                Status = ModerationStatus.Pending,
                CreatedByUserId = userId,
                CreatedAt = DateTime.Now
            };
            _tagDefinitions.Add(definition);
            createdNew = true;
            IncrementUserNewTag(userId);
            _notificationService.CreateSystemNotification("admin-internal", "Новый тег на модерацию", definition.Name);
        }
        else
        {
            definition = existing;
        }

        var assignment = new AuctionTagAssignment
        {
            AuctionId = auctionId,
            TagDefinitionId = definition.Id,
            RawInput = rawTag.Trim(),
            NormalizedTag = normalized,
            Category = definition.Category,
            Status = ModerationStatus.Pending,
            CreatedByUserId = userId,
            CreatedAt = DateTime.Now
        };
        _assignments.Add(assignment);

        if (definition.Status == ModerationStatus.Approved)
        {
            assignment.Status = ModerationStatus.Approved;
            assignment.ModeratedByAdminId = "admin-internal";
            assignment.ModeratedAt = DateTime.Now;
            definition.UsageCount++;
            RecordModerationLog("admin-internal", "ApproveAssignment", nameof(AuctionTagAssignment), assignment.Id, definition.Name, auctionId: assignment.AuctionId);
            var creator = _userService.GetUserById(assignment.CreatedByUserId);
            var auctionInfo = _auctionService.GetAuctionById(assignment.AuctionId);
            if (creator != null && auctionInfo != null)
                _notificationService.CreateSystemNotification(creator.Id, "Тег принят для лота", $"\"{definition.Name}\" для \"{auctionInfo.Title}\"");

            return new AuctionTagAttachResult
            {
                Success = true,
                UsedExistingApprovedTag = true,
                CreatedNewTagDefinition = false,
                TagDefinition = definition,
                Assignment = assignment
            };
        }
        else
        {
            _notificationService.CreateSystemNotification("admin-internal", "Новый тег и назначение", definition.Name);
            return new AuctionTagAttachResult
            {
                Success = true,
                UsedExistingApprovedTag = false,
                CreatedNewTagDefinition = createdNew,
                TagDefinition = definition,
                Assignment = assignment
            };
        }
    }

    public List<Auction> FilterAuctionsByTags(List<string> tagNames, out Dictionary<string, int> relevanceScore, bool matchAll = true)
    {
        relevanceScore = new Dictionary<string, int>();
        if (tagNames == null || tagNames.Count == 0) return _auctionService.GetActiveAuctions();
        var normalizedQueries = tagNames
            .Select(t => Normalize(MapSynonym(t)))
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct()
            .ToList();

        if (!normalizedQueries.Any()) return _auctionService.GetActiveAuctions();

        var approvedTagDefinitionIds = _tagDefinitions
            .Where(t => t.Status == ModerationStatus.Approved && normalizedQueries.Any(q => t.NormalizedName.Contains(q) || q.Contains(t.NormalizedName)))
            .Select(t => t.Id)
            .ToHashSet();

        var approvedAssignments = _assignments
            .Where(a => a.Status == ModerationStatus.Approved &&
                        (approvedTagDefinitionIds.Contains(a.TagDefinitionId) ||
                         normalizedQueries.Any(q => a.NormalizedTag.Contains(q) || q.Contains(a.NormalizedTag))))
            .GroupBy(a => a.AuctionId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.TagDefinitionId).ToHashSet());

        var all = _auctionService.GetActiveAuctions();
        var filteredWithScores = new List<(Auction auction, int score)>();
        foreach (var a in all)
        {
            approvedAssignments.TryGetValue(a.Id, out var set);
            var matches = set?.Count ?? 0;
            if ((matchAll && set != null && set.Count >= approvedTagDefinitionIds.Count) || (!matchAll && matches > 0))
            {
                filteredWithScores.Add((a, matches));
                relevanceScore[a.Id] = matches;
            }
        }

        var filtered = filteredWithScores
            .OrderByDescending(x => x.score)
            .ThenByDescending(x => x.auction.ViewsCount)
            .Select(x => x.auction)
            .ToList();
        return filtered;
    }

    public void RecordTagQuery(IEnumerable<string> tagNames)
    {
        foreach (var t in tagNames)
        {
            var key = Normalize(MapSynonym(t));
            _tagQueryCounter.TryGetValue(key, out var c);
            _tagQueryCounter[key] = c + 1;
        }
    }

    public List<(TagDefinition tag, int searchCount)> GetTagAnalytics(int minUsage = 0)
    {
        var list = new List<(TagDefinition tag, int searchCount)>();
        foreach (var t in _tagDefinitions)
        {
            if (t.UsageCount < minUsage) continue;
            _tagQueryCounter.TryGetValue(t.NormalizedName, out var sc);
            list.Add((t, sc));
        }
        return list
            .OrderByDescending(x => x.tag.UsageCount)
            .ThenByDescending(x => x.searchCount)
            .ThenBy(x => x.tag.Name)
            .ToList();
    }

    public List<TagModerationLogEntry> GetModerationLogEntries(int top = 50)
    {
        return _moderationLogs
            .OrderByDescending(x => x.Timestamp)
            .Take(top)
            .ToList();
    }

    public List<AuctionTagAssignment> GetApprovedTagAssignmentsForAuction(string auctionId)
    {
        return _assignments
            .Where(a => a.AuctionId == auctionId && a.Status == ModerationStatus.Approved)
            .ToList();
    }

    private void RecordModerationLog(string adminId, string action, string entityType, string entityId, string targetTagName, string? auctionId = null, string? reason = null)
    {
        _moderationLogs.Add(new TagModerationLogEntry
        {
            AdminId = adminId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            TargetTagName = targetTagName,
            AuctionId = auctionId,
            Reason = reason,
            Timestamp = DateTime.Now
        });
    }

    public bool ApproveTagDefinition(string adminId, string tagDefinitionId, string? newCategory = null)
    {
        var def = _tagDefinitions.FirstOrDefault(t => t.Id == tagDefinitionId);
        if (def == null) return false;
        if (!string.IsNullOrWhiteSpace(newCategory) && SystemCategories.Contains(newCategory))
            def.Category = newCategory;
        def.Status = ModerationStatus.Approved;
        def.RejectReason = null;
        def.ModeratedByAdminId = adminId;
        def.ModeratedAt = DateTime.Now;
        RecordModerationLog(adminId, "ApproveDefinition", nameof(TagDefinition), def.Id, def.Name);
        var creator = _userService.GetUserById(def.CreatedByUserId);
        if (creator != null)
            _notificationService.CreateSystemNotification(creator.Id, "Тег одобрен", $"Тег \"{def.Name}\" одобрен");

        var pendingAssignments = _assignments.Where(a => a.TagDefinitionId == def.Id && a.Status == ModerationStatus.Pending).ToList();
        foreach (var assignment in pendingAssignments)
        {
            ApproveAssignment(adminId, assignment.Id);
        }

        return true;
    }

    public bool RejectTagDefinition(string adminId, string tagDefinitionId, string reason)
    {
        var def = _tagDefinitions.FirstOrDefault(t => t.Id == tagDefinitionId);
        if (def == null) return false;
        def.Status = ModerationStatus.Rejected;
        def.ModeratedByAdminId = adminId;
        def.ModeratedAt = DateTime.Now;
        def.RejectReason = reason;
        RecordModerationLog(adminId, "RejectDefinition", nameof(TagDefinition), def.Id, def.Name, reason: reason);
        var creator = _userService.GetUserById(def.CreatedByUserId);
        if (creator != null)
            _notificationService.CreateSystemNotification(creator.Id, "Тег отклонён", $"Тег \"{def.Name}\" отклонён: {reason}");

        var pendingAssignments = _assignments.Where(a => a.TagDefinitionId == def.Id && a.Status == ModerationStatus.Pending).ToList();
        foreach (var assignment in pendingAssignments)
        {
            RejectAssignment(adminId, assignment.Id, reason);
        }

        return true;
    }

    public bool ApproveAssignment(string adminId, string assignmentId)
    {
        var asg = _assignments.FirstOrDefault(a => a.Id == assignmentId);
        if (asg == null) return false;
        var def = _tagDefinitions.FirstOrDefault(t => t.Id == asg.TagDefinitionId);
        if (def == null) return false;
        asg.Status = ModerationStatus.Approved;
        asg.ModeratedByAdminId = adminId;
        asg.ModeratedAt = DateTime.Now;
        def.UsageCount++;
        RecordModerationLog(adminId, "ApproveAssignment", nameof(AuctionTagAssignment), asg.Id, def.Name, auctionId: asg.AuctionId);
        var auction = _auctionService.GetAuctionById(asg.AuctionId);
        var creator = _userService.GetUserById(asg.CreatedByUserId);
        if (auction != null && creator != null)
            _notificationService.CreateSystemNotification(creator.Id, "Тег принят для лота", $"\"{def.Name}\" для \"{auction.Title}\"");
        return true;
    }

    public bool RejectAssignment(string adminId, string assignmentId, string reason)
    {
        var asg = _assignments.FirstOrDefault(a => a.Id == assignmentId);
        if (asg == null) return false;
        asg.Status = ModerationStatus.Rejected;
        asg.ModeratedByAdminId = adminId;
        asg.ModeratedAt = DateTime.Now;
        asg.RejectReason = reason;
        var def = _tagDefinitions.FirstOrDefault(t => t.Id == asg.TagDefinitionId);
        RecordModerationLog(adminId, "RejectAssignment", nameof(AuctionTagAssignment), asg.Id, def?.Name ?? string.Empty, auctionId: asg.AuctionId, reason: reason);
        var auction = _auctionService.GetAuctionById(asg.AuctionId);
        var creator = _userService.GetUserById(asg.CreatedByUserId);
        if (auction != null && creator != null && def != null)
            _notificationService.CreateSystemNotification(creator.Id, "Тег отклонён для лота", $"\"{def.Name}\" для \"{auction.Title}\": {reason}");
        return true;
    }

    public List<TagDefinition> GetApprovedDefinitions() => _tagDefinitions.Where(t => t.Status == ModerationStatus.Approved).ToList();
    public List<string> GetApprovedTagNamesForAuction(string auctionId)
    {
        var approved = _assignments.Where(a => a.AuctionId == auctionId && a.Status == ModerationStatus.Approved)
            .Select(a => a.TagDefinitionId)
            .ToHashSet();
        return _tagDefinitions.Where(t => approved.Contains(t.Id)).Select(t => t.Name).OrderBy(s => s).ToList();
    }
}
