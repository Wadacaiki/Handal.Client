using Handal.Client.Models;
using Microsoft.JSInterop;

namespace Handal.Client.Services;

/// <summary>
/// Главный сервис платформы (использует все остальные сервисы)
/// </summary>
public class AuctionPlatformService
{
    public UserService UserService { get; }
    public AuctionService AuctionService { get; }
    public BidService BidService { get; }
    public NotificationService NotificationService { get; }
    public ChatService ChatService { get; }
    public TagService TagService { get; }

    public PersistenceService Persistence { get; }

    public IEmailService EmailService { get; }

    public AuctionPlatformService(HttpClient httpClient, IJSRuntime js)
    {
        EmailService = new EmailJsService(httpClient);
        NotificationService = new NotificationService();
        UserService = new UserService(EmailService, NotificationService);
        AuctionService = new AuctionService();
        ChatService = new ChatService();
        BidService = new BidService(AuctionService, UserService, NotificationService, EmailService);
        // Передаём ссылку BidService в AuctionService чтобы завершение аукциона могло получить реальные ставки
        AuctionService.SetBidService(BidService);
        TagService = new TagService(NotificationService, AuctionService, UserService);

        // Автоматическое сохранение при изменении состояния пользователей (логин/логаут/баланс)
        UserService.OnAuthStateChanged += () => SaveState();

        // Попытка загрузки состояния из JSON; если нет — инициализируем демо-данные
        Persistence = new PersistenceService(js);
        var loaded = false;
        try
        {
            loaded = Persistence.Load(this);
        }
        catch
        {
            loaded = false;
        }
        if (!loaded)
        {
            InitializeDemoData();
        }
        else
        {
            var auctions = AuctionService.GetAllAuctions();
            foreach (var a in auctions)
            {
                if (string.IsNullOrWhiteSpace(a.Image))
                    a.Image = MapImage(a.Title);
                a.Category = (a.Category ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(a.Category))
                    a.Category = MapCategory(a.Title);
            }

            var dedup = new Dictionary<string, Auction>();
            var keep = new List<Auction>();
            foreach (var a in auctions)
            {
                if (!string.IsNullOrWhiteSpace(a.SellerId) && a.SellerId.StartsWith("seller-", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(a.Title))
                {
                    var key = $"{a.SellerId}||{a.Title.Trim().ToLowerInvariant()}";
                    if (!dedup.TryGetValue(key, out var existing) || a.StartedAt > existing.StartedAt)
                        dedup[key] = a;
                }
                else
                {
                    keep.Add(a);
                }
            }
            keep.AddRange(dedup.Values);

            // АВТО-ПРОДЛЕНИЕ: Обновляем время для системных лотов, чтобы сайт всегда выглядел живым
            // Если время истекло, добавляем от 12 до 24 часов
            var rndRef = new Random();
            foreach (var a in keep)
            {
                if (!string.IsNullOrEmpty(a.SellerId) && a.SellerId.StartsWith("seller-"))
                {
                    if (a.EndsAt <= DateTime.Now.AddHours(1)) // Если истекло или истечет в течение часа
                    {
                        var hours = rndRef.Next(12, 24);
                        a.EndsAt = DateTime.Now.AddHours(hours).AddMinutes(rndRef.Next(0, 60));
                        a.Status = AuctionStatus.Active; // Гарантируем, что лот активен
                    }
                }
            }

            var auctionsField = AuctionService.GetType().GetField("_auctions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (auctionsField != null)
            {
                var list = auctionsField.GetValue(AuctionService) as List<Auction>;
                if (list != null)
                {
                    list.Clear();
                    list.AddRange(keep);
                }
            }

            SaveState();
        }

        TagService.EnsureSuggestedTagsForExistingAuctions();
        SaveState();
    }

    /// <summary>
    /// Сохранить текущее состояние платформы в JSON
    /// </summary>
    public void SaveState()
    {
        try
        {
            Persistence.Save(this);
        }
        catch
        {
            // Нежёсткое сохранение — ошибки не должны ломать работу
        }
    }

    /// <summary>
    /// Досрочно завершить аукцион продавцом с полной обработкой победителя/проигравших и уведомлений.
    /// </summary>
    public bool EndAuctionBySellerAndSettle(string auctionId, string sellerId)
    {
        var result = AuctionService.EndAuctionBySellerWithResult(auctionId, sellerId);
        if (!result.Success || result.Auction == null)
            return false;

        var auction = result.Auction;
        var seller = UserService.GetUserById(sellerId);
        var winnerId = result.WinnerBid?.UserId;
        var winner = string.IsNullOrWhiteSpace(winnerId) ? null : UserService.GetUserById(winnerId);

        if (winner != null)
        {
            // Победитель: внутренняя почта + уведомление о выигрыше
            NotificationService.CreateSystemNotification(
                winner.Id,
                "Поздравляем с новым приобретенным товаром",
                "поздравляем с новым приобретенным товаром, продолжайте в том же духе");
            try
            {
                EmailService.SendEmailAsync(
                    winner.Email,
                    "Поздравляем с новым приобретенным товаром",
                    "поздравляем с новым приобретенным товаром, продолжайте в том же духе",
                    winner.FullName).GetAwaiter().GetResult();
            }
            catch
            {
            }
        }

        // Проигравшие: "к сожаленб не в этот раз"
        foreach (var loserId in result.LosingBidderIds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (winner != null && loserId == winner.Id)
                continue;

            var loser = UserService.GetUserById(loserId);
            if (loser == null)
                continue;

            NotificationService.CreateSystemNotification(
                loser.Id,
                "Аукцион завершён",
                "к сожаленб не в этот раз");
            try
            {
                EmailService.SendEmailAsync(
                    loser.Email,
                    "Аукцион завершён",
                    "к сожаленб не в этот раз",
                    loser.FullName).GetAwaiter().GetResult();
            }
            catch
            {
            }
        }

        if (seller != null)
        {
            var winnerName = winner?.FullName ?? "покупатель";
            NotificationService.CreateSystemNotification(
                seller.Id,
                "Товар продан",
                $"у вас приобрел товар {winnerName}, желаем вам дальнейших игр");
            try
            {
                EmailService.SendEmailAsync(
                    seller.Email,
                    "Товар продан",
                    $"у вас приобрел товар {winnerName}, желаем вам дальнейших игр",
                    seller.FullName).GetAwaiter().GetResult();
            }
            catch
            {
            }
        }

        SaveState();
        return true;
    }

    private static string MapImage(string title)
    {
        var t = title.ToLowerInvariant();
        if (t.Contains("винтажные часы")) return "/images/lots/винтажныечасы.jpg";
        if (t.Contains("rolex") || t.Contains("швейцарские часы")) return "/images/lots/швейцарскиечасы.jpg";
        if (t.Contains("фотокамера") && t.Contains("киев")) return "/images/lots/фотокамеракиев.jpg";
        if (t.Contains("виниловая пластинка")) return "/images/lots/виниловаяпластинка.jpg";
        if (t.Contains("картина маслом")) return "/images/lots/картинамаслом.jpg";
        if (t.Contains("кувшин") || t.Contains("кувшины")) return "/images/lots/глиняныекувшины.jpg";
        if (t.Contains("мебель")) return "/images/lots/стараямебель.jpg";
        if (t.Contains("книг") || t.Contains("книга")) return "/images/lots/книги.jpg";
        if (t.Contains("серебряное зеркало") || t.Contains("зеркал")) return "/images/lots/серебряноезеркало.jpg";
        if (t.Contains("орден")) return "/images/lots/орден.jpg";
        if (t.Contains("люстра")) return "/images/lots/люстра.jpg";
        if (t.Contains("компас")) return "/images/lots/компас.jpg";
        if (t.Contains("монета")) return "/images/lots/монета.jpg";
        if (t.Contains("опера")) return "/images/lots/опера.jpg";
        if (t.Contains("статуэтка") && t.Contains("мрамор")) return "/images/lots/мраморнаястатуетка.jpg";
        if (t.Contains("кулон")) return "/images/lots/кулон.jpg";
        if (t.Contains("шпага")) return "/images/lots/шпага.jpg";
        if (t.Contains("немецкий фарфор") || (t.Contains("фарфор") && t.Contains("meissen"))) return "/images/lots/немецкийфарфор.jpg";
        if (t.Contains("мозаика")) return "/images/lots/мозаика.jpg";
        if (t.Contains("ваза")) return "/images/lots/императорскаяваза.jpg";
        if (t.Contains("духи") || t.Contains("парфюм")) return "/images/lots/духи.jpg";
        if (t.Contains("будильник")) return "/images/lots/будильник.jpg";
        if (t.Contains("подсвечник")) return "/images/lots/подсвечник.jpg";
        if (t.Contains("икона")) return "/images/lots/икона.jpg";
        if (t.Contains("виски")) return "/images/lots/виски.jpg";
        if (t.Contains("марка")) return "/images/lots/маркассср.jpg";
        if (t.Contains("кольцо")) return "/images/lots/кольцо.jpg";
        if (t.Contains("керамический кубок") || (t.Contains("керамич") && t.Contains("кубок"))) return "/images/lots/керамическийкубок.jpg";
        if (t.Contains("граммофон")) return "/images/lots/граммофон.jpg";
        if (t.Contains("портрет")) return "/images/lots/портреткисти.jpg";
        if (t.Contains("килим")) return "/images/lots/килим.jpg";
        if (t.Contains("слоновой кости") || t.Contains("нэцкэ")) return "/images/lots/статуэткаизслоновойкости.jpg";
        if (t.Contains("фарфоровая посуда") || t.Contains("посуда")) return "/images/lots/фарфороваяпосуда.jpg";
        if (t.Contains("маска")) return "/images/lots/венецианскаямаска.jpg";
        if (t.Contains("свитки")) return "/images/lots/свитки.jpg";
        if (t.Contains("брошь")) return "/images/lots/брошь.jpg";
        if (t.Contains("самовар")) return "/images/lots/самовар.jpg";
        if (t.Contains("Пушкина")) return "/images/lots/книгапушкина.jpg";
        if (t.Contains("каподимонте")) return "/images/lots/каподимонте.jpg";
        if (t.Contains("часы")) return "/images/lots/винтажныечасы.jpg";
        return "/images/lots/винтажныечасы.jpg";
    }

    private static string MapCategory(string title)
    {
        var t = title.ToLowerInvariant();
        if (t.Contains("часы") || t.Contains("будильник")) return "Часы";
        if (t.Contains("кольцо") || t.Contains("кулон") || t.Contains("брошь") || t.Contains("ювел")) return "Украшения";
        if (t.Contains("книга") || t.Contains("издание") || t.Contains("пушкина")) return "Книги";
        if (t.Contains("виниловая") || t.Contains("опера") || t.Contains("граммофон") || t.Contains("музык")) return "Музыка";
        if (t.Contains("шпага") || t.Contains("оруж")) return "Оружие";
        if (t.Contains("мебель") || t.Contains("люстра") || t.Contains("самовар")) return "Мебель";
        if (t.Contains("икона") || t.Contains("компас") || t.Contains("монета") || t.Contains("марка") || t.Contains("свитки") || t.Contains("истор")) return "История";
        if (t.Contains("картина") || t.Contains("портрет") || t.Contains("мозаика") || t.Contains("маска") || t.Contains("статуэтка") || t.Contains("каподимонте")) return "Искусство";
        if (t.Contains("килим") || t.Contains("ваза") || t.Contains("фарфор") || t.Contains("керамич") || t.Contains("кувшин") || t.Contains("нэцкэ")) return "Этнос";
        if (t.Contains("орден") || t.Contains("зеркало") || t.Contains("духи") || t.Contains("виски")) return "Антиквариат";
        // fallback
        return "Антиквариат";
    }

    /// <summary>
    /// Инициализировать демо-данные для тестирования
    /// </summary>
    private void InitializeDemoData()
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        (UserService.GetType().GetField("_users", flags)?.GetValue(UserService) as List<User>)?.Clear();
        (AuctionService.GetType().GetField("_auctions", flags)?.GetValue(AuctionService) as List<Auction>)?.Clear();
        (AuctionService.GetType().GetField("_history", flags)?.GetValue(AuctionService) as List<AuctionHistory>)?.Clear();
        (BidService.GetType().GetField("_bids", flags)?.GetValue(BidService) as List<Bid>)?.Clear();
        (NotificationService.GetType().GetField("_notifications", flags)?.GetValue(NotificationService) as List<Notification>)?.Clear();
        (ChatService.GetType().GetField("_messages", flags)?.GetValue(ChatService) as List<ChatMessage>)?.Clear();
        // Создать тестовых пользователей
        var seller = new User
        {
            Id = "seller-1",
            Email = "seller@example.com",
            Password = "123456",
            FullName = "Иван Петров",
            DateOfBirth = new DateTime(1990, 5, 15),
            IsCardLinked = true,
            CardMask = "**** **** **** 1234",
            IsEmailVerified = true,
            Balance = 500000m,
            Rating = 4.8m,
            SalesCount = 125
        };

        var buyer = new User
        {
            Id = "buyer-1",
            Email = "buyer@example.com",
            Password = "123456",
            FullName = "Мария Сидорова",
            DateOfBirth = new DateTime(1995, 8, 20),
            IsCardLinked = true,
            CardMask = "**** **** **** 5678",
            IsEmailVerified = true,
            Balance = 1000000m,
            Rating = 4.5m,
            PurchaseCount = 42
        };

        var admin = new User
        {
            Id = "admin-1",
            Email = "admin@example.com",
            Password = "admin123",
            FullName = "Системный администратор",
            DateOfBirth = new DateTime(1985, 1, 1),
            IsEmailVerified = true,
            IsAdmin = true,
            Balance = 0m,
            Rating = 5m
        };

        // Добавить пользователей в сервис
        var usersField = UserService.GetType().GetField("_users",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (usersField != null)
        {
            var users = usersField.GetValue(UserService) as List<User>;
            users?.Add(seller);
            users?.Add(buyer);
            users?.Add(admin);
        }

        var extraSellers = new List<User>
        {
            new User { Id = "seller-2", Email = "antique.master@example.com", Password = "123456", FullName = "Анна Романова", DateOfBirth = new DateTime(1988, 3, 12), IsCardLinked = true, CardMask = "**** **** **** 1111", IsEmailVerified = true, Balance = 250000m, Rating = 4.7m, SalesCount = 64 },
            new User { Id = "seller-3", Email = "art.collection@example.com", Password = "123456", FullName = "Павел Синицын", DateOfBirth = new DateTime(1982, 11, 2), IsCardLinked = true, CardMask = "**** **** **** 2222", IsEmailVerified = true, Balance = 420000m, Rating = 4.9m, SalesCount = 210 },
            new User { Id = "seller-4", Email = "rare.items@example.com", Password = "123456", FullName = "Маргарита Белова", DateOfBirth = new DateTime(1991, 1, 27), IsCardLinked = true, CardMask = "**** **** **** 3333", IsEmailVerified = true, Balance = 180000m, Rating = 4.6m, SalesCount = 47 },
            new User { Id = "seller-5", Email = "classic.time@example.com", Password = "123456", FullName = "Дмитрий Орлов", DateOfBirth = new DateTime(1985, 6, 8), IsCardLinked = true, CardMask = "**** **** **** 4444", IsEmailVerified = true, Balance = 360000m, Rating = 4.8m, SalesCount = 128 }
        };
        if (usersField != null)
        {
            var users = usersField.GetValue(UserService) as List<User>;
            if (users != null) users.AddRange(extraSellers);
        }
        var sellersPool = new List<User> { seller };
        sellersPool.AddRange(extraSellers);

        // Создать 35 тестовых аукционов
        var auctionTitles = new[]
        {
            "NOMOS Танго - Винтажные часы 1985 года",
            "Швейцарские часы Rolex Submariner Gold",
            "Винтажная советская фотокамера Киев",
            "Редкая виниловая пластинка Pink Floyd",
            "Картина маслом неизвестного художника XVIII века",
            "Древний глиняный кувшин из Египта",
            "Старая французская мебель эпохи Людовика XVI",
            "Коллекционное издание книги J.R.R. Tolkien",
            "Антикварное серебряное зеркало XIX века",
            "Орден Красного Знамени СССР",
            "Старинная люстра Венецианского стекла",
            "Военный компас времен Второй Мировой",
            "Редкая монета: Империал Александра III",
            "Итальянская опера в оригинальной раме 1920г",
            "Греческая статуэтка из белого мрамора",
            "Золотой кулон с натуральным аметистом",
            "Средневековое боевое оружие - шпага",
            "Немецкий фарфор XIX века Meissen",
            "Древняя мозаика из Помпеи",
            "Императорская китайская ваза династии Цин",
            "Редкий французский духи парфюм 1950х",
            "Старинный механический будильник Junghans",
            "Антикварный подсвечник из латуни",
            "Византийская икона на деревянной основе",
            "Премиум виски Macallan 1952 года",
            "Редкая марка СССР филателия",
            "Кольцо с бриллиантом и изумрудом",
            "Испанский керамический кубок 1600х годов",
            "Старинный музыкальный граммофон",
            "Портрет кисти неизвестного академиста",
            "Турецкий килим ручной работы XVII века",
            "Статуэтка из слоновой кости Нэцкэ",
            "Королевская фарфоровая посуда Sevres",
            "Венецианская маска оригинальная",
            "Древние свитки на пергаменте",
            "Антикварная брошь с сапфиром",
            "Старинный самовар Тула",
            "Книга Пушкина прижизненное издание",
            "Фарфоровая статуэтка Каподимонте"
        };

        var descriptions = new[]
        {
            "Аутентичные часы, проверены экспертом. Рабочее состояние, требуется чистка.",
            "В идеальном состоянии, с оригинальным паспортом. Коллекционный вариант.",
            "Редкая модель, все функции работают. История владения подтверждена.",
            "Альбом в отличном состоянии, виниловая пластинка без трещин и потертостей.",
            "Написана маслом на холсте. Нужна реставрация. Интересный исторический артефакт.",
            "Древний предмет из музейной коллекции. Идеален для коллекционеров.",
            "Оригинальная французская мебель, требуется реставрация. Роскошный дизайн.",
            "Первое издание, редкое и ценное. Все страницы в отличном состоянии.",
            "Серебро 925 пробы. Редкий дизайн. Зеркало отражает прекрасно.",
            "Советский орден с документацией. Прекрасный подарок коллекционеру истории.",
            "Люстра из оригинального Венецианского стекла. Требуется реставрация электрики.",
            "Исторический компас военных времен. Все функции работают, редкий артефакт.",
            "Редкая монета, чеканка 1913 года. Идеально сохранилась в капсуле.",
            "Оригинальная опера, подписанная художником. Красивая деревянная рама.",
            "Подлинная греческая статуэтка. Пронесена через столетия. Уникальна.",
            "Натуральный аметист хорошего качества. Позолоченный кулон ручной работы.",
            "Средневековая шпага, заточена. Идеальна для музея или коллекции оружия.",
            "Фарфор Meissen высочайшего качества. Редкий рисунок, хорошо сохранился.",
            "Подлинная мозаика из древних Помпеи. Настоящая история в руках.",
            "Императорская ваза эпохи Цин. Редкая раскраска. Инвестиция в искусство.",
            "Оригинальный флакон, парфюм 1950х годов. Редкий аромат. Коллекционный.",
            "Механический будильник Junghans в рабочем состоянии. Винтажный звук.",
            "Латунный подсвечник ручной работы. Идеален для декора интерьера.",
            "Икона написана на дереве. Золотой фон. Старинная византийская техника.",
            "Виски Macallan 1952 года. Редкий винтаж. Инвестиция напиток.",
            "Редкая советская марка. Филателист найдет шедевр. Каталогная стоимость 5000+.",
            "Кольцо с крупным бриллиантом и изумрудом. Золото 585 пробы. Ювелирный шедевр.",
            "Испанская керамика 1600х годов. Расписанная вручную. Редкая коллекция.",
            "Граммофон механический 1920х годов. Музыка старины. Работает отлично.",
            "Портрет кисти академиста XIX века. Большой размер. Требует рамы для дома.",
            "Килим ручной работы из Турции. Натуральные краски. Идеален для пола.",
            "Нэцкэ из слоновой кости. Редкая миниатюра. Подлинный японский артефакт.",
            "Королевская посуда фабрики Sevres. Полный сервиз. Редкий рисунок.",
            "Маска оригинальная венецианская из папье-маше. Ручная роспись. Подлинник.",
            "Древние свитки на пергаменте. Текст на древнегреческом. Историческая ценность.",
            "Брошь с сапфиром в золоте 750 пробы. Авторская работа.",
            "Самовар конца XIX века, клеймо Тула. Отличное состояние.",
            "Редкое прижизненное издание. Коллекционная ценность.",
            "Статуэтка Каподимонте ручной работы. Идеальное состояние."
        };

        var rnd = new Random();
        decimal PriceForCategory(string cat)
        {
            if (cat == "Искусство") return rnd.Next(30000, 200000);
            if (cat == "Украшения") return rnd.Next(10000, 100000);
            if (cat == "Оружие") return rnd.Next(20000, 120000);
            if (cat == "Часы") return rnd.Next(5000, 30000);
            if (cat == "Антиквариат") return rnd.Next(10000, 80000);
            if (cat == "Книги") return rnd.Next(3000, 20000);
            if (cat == "Этнос") return rnd.Next(8000, 50000);
            if (cat == "Мебель") return rnd.Next(15000, 70000);
            if (cat == "История") return rnd.Next(7000, 50000);
            if (cat == "Музыка") return rnd.Next(5000, 25000);
            return rnd.Next(5000, 30000);
        }

        for (int i = 0; i < auctionTitles.Length; i++)
        {
            var cat = MapCategory(auctionTitles[i]);
            var startPrice = PriceForCategory(cat);
            var inc = (decimal)rnd.NextDouble() * startPrice * 0.35m;
            var current = Math.Ceiling(startPrice + inc);
            var sellerPick = sellersPool[rnd.Next(sellersPool.Count)];
            var durationHours = rnd.Next(6, 72);
            var extraMinutes = rnd.Next(0, 59);
            var auction = new Auction
            {
                SellerId = sellerPick.Id,
                Title = auctionTitles[i],
                Description = descriptions[i],
                Category = cat,
                StartPrice = startPrice,
                CurrentBid = current,
                Image = MapImage(auctionTitles[i]),
                StartedAt = DateTime.Now.AddHours(-i),
                EndsAt = DateTime.Now.AddHours(durationHours).AddMinutes(extraMinutes),
                IsFeatured = i < 5,
                IsCharitable = false,
                CharityPercent = 0,
                Status = AuctionStatus.Active,
                BidsCount = rnd.Next(0, 13),
                ViewsCount = rnd.Next(80, 1200)
            };
            var auctionsField = AuctionService.GetType().GetField("_auctions",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (auctionsField != null)
            {
                var auctionsList = auctionsField.GetValue(AuctionService) as List<Auction>;
                if (auctionsList != null)
                {
                    auctionsList.Add(auction);
                }
            }
        }

        var auctionsFieldFinal = AuctionService.GetType().GetField("_auctions",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (auctionsFieldFinal != null)
        {
            var auctionsList = auctionsFieldFinal.GetValue(AuctionService) as List<Auction>;
            if (auctionsList != null)
            {
                var candidates = auctionsList.Where(a => a.Status == AuctionStatus.Active).ToList();
                var pickCount = Math.Min(10, candidates.Count);
                for (var k = 0; k < pickCount; k++)
                {
                    var idx = rnd.Next(candidates.Count);
                    var a = candidates[idx];
                    candidates.RemoveAt(idx);
                    a.IsCharitable = true;
                    a.CharityPercent = 10m;
                }

                var chatAuthors = new List<User>
                {
                    new User { Id = "chat-user-1", FullName = "Артём", Email = "artem@demo.chat", Password = "x", DateOfBirth = new DateTime(1994, 1, 12), IsEmailVerified = true },
                    new User { Id = "chat-user-2", FullName = "Ксения", Email = "ksenia@demo.chat", Password = "x", DateOfBirth = new DateTime(1997, 6, 3), IsEmailVerified = true },
                    new User { Id = "chat-user-3", FullName = "Михаил", Email = "mikhail@demo.chat", Password = "x", DateOfBirth = new DateTime(1990, 9, 18), IsEmailVerified = true },
                    new User { Id = "chat-user-4", FullName = "Елена", Email = "elena@demo.chat", Password = "x", DateOfBirth = new DateTime(1989, 11, 24), IsEmailVerified = true }
                };
                var chatPhrases = new[]
                {
                    "Подскажите, есть ли дефекты/сколы?",
                    "Отправка по России возможна?",
                    "Можно фото поближе, особенно маркировку?",
                    "Лот оригинальный? Есть документы/провенанс?",
                    "Интересно, какая история у предмета?",
                    "Ставка бодро растёт, удачи всем!",
                    "Торг уместен вне аукциона или строго тут?",
                    "Очень редкая вещь, подписываюсь.",
                    "Состояние отличное, видно по фото.",
                    "Кто-нибудь уже делал экспертизу?"
                };
                var seedCount = Math.Min(60, auctionsList.Count * 2);
                for (var i = 0; i < seedCount; i++)
                {
                    var auction = auctionsList[rnd.Next(auctionsList.Count)];
                    var author = chatAuthors[rnd.Next(chatAuthors.Count)];
                    var text = chatPhrases[rnd.Next(chatPhrases.Length)];
                    ChatService.AddMessage(new ChatMessage
                    {
                        AuctionId = auction.Id,
                        AuthorId = author.Id,
                        Author = author,
                        Text = text,
                        SentAt = DateTime.Now.AddMinutes(-rnd.Next(5, 800))
                    });
                }
            }
        }
    }

    /// <summary>
    /// Получить основную информацию о платформе
    /// </summary>
    public PlatformStats GetPlatformStats()
    {
        var baseStats = new PlatformStats
        {
            TotalUsers = UserService.GetAllUsers().Count,
            TotalAuctions = AuctionService.GetAllAuctions().Count,
            ActiveAuctions = AuctionService.GetActiveAuctions().Count,
            TotalVolume = AuctionService.GetAllAuctions()
                .Where(a => a.Status == AuctionStatus.Completed)
                .Sum(a => a.FinalPrice ?? 0)
        };

        // Умножаем для красоты статистики (глобальные показатели)
        return new PlatformStats
        {
            TotalUsers = baseStats.TotalUsers * 420 + 1200,
            TotalAuctions = baseStats.TotalAuctions * 150 + 5000,
            ActiveAuctions = baseStats.ActiveAuctions,
            TotalVolume = baseStats.TotalVolume > 0 ? baseStats.TotalVolume * 1000 : 75000000
        };
    }

    /// <summary>
    /// Получить рекомендуемые лоты
    /// </summary>
    public List<Auction> GetRecommendedAuctions(int count = 6)
    {
        return AuctionService.GetActiveAuctions()
            .OrderByDescending(a => a.IsFeatured)
            .ThenByDescending(a => a.ViewsCount)
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// Поиск аукционов
    /// </summary>
    public List<Auction> SearchAuctions(string query)
    {
        var lower = query.ToLower();
        return AuctionService.GetActiveAuctions()
            .Where(a => a.Title.ToLower().Contains(lower) ||
                       a.Description.ToLower().Contains(lower) ||
                       a.Category.ToLower().Contains(lower))
            .ToList();
    }

    /// <summary>
    /// Получить категории
    /// </summary>
    public List<string> GetCategories()
    {
        return AuctionService.GetAllAuctions()
            .Select(a => a.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToList();
    }
}

/// <summary>
/// Статистика платформы
/// </summary>
public class PlatformStats
{
    public int TotalUsers { get; set; }
    public int TotalAuctions { get; set; }
    public int ActiveAuctions { get; set; }
    public decimal TotalVolume { get; set; }
}
