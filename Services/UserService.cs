using Handal.Client.Models;

namespace Handal.Client.Services;

/// <summary>
/// Сервис для управления пользователями
/// </summary>
public class UserService
{
    private List<User> _users = new();
    private List<VerificationCodeEntry> _verificationCodes = new();
    private List<BalanceTransaction> _balanceTransactions = new();
    // Текущее состояние аутентификации
    public User? CurrentUser { get; private set; }

    // Событие для уведомления об изменении состояния аутентификации
    public event Action? OnAuthStateChanged;
    // Событие — запрос открытия модалки авторизации (например, с других страниц)
    public event Action? OnOpenAuthRequested;
    public event Action? OnDebugStateChanged;

    private readonly IEmailService _emailService;
    private readonly NotificationService? _notificationService;

    public UserService(IEmailService emailService, NotificationService? notificationService = null)
    {
        _emailService = emailService;
        _notificationService = notificationService;
    }

    /// <summary>
    /// Регистрация нового пользователя
    /// </summary>
    public async Task<(bool success, string message, User? user)> RegisterAsync(
        string email,
        string password,
        string fullName,
        DateTime dateOfBirth)
    {
        email = email.Trim().ToLower();
        fullName = fullName.Trim();
        if (string.IsNullOrWhiteSpace(fullName))
            return (false, "Никнейм обязателен", null);

        if (_users.Any(u => u.FullName.Equals(fullName, StringComparison.OrdinalIgnoreCase)))
            return (false, "Профиль с таким никнеймом уже существует", null);

        if (_users.Any(u => u.Email == email && u.Password == password))
            return (false, "Для этого email такой пароль уже используется", null);

        // Проверка возраста (18 лет и старше)
        var today = DateTime.Today;
        if (dateOfBirth.Date > today)
            return (false, "Некорректная дата рождения", null);

        var age = today.Year - dateOfBirth.Year;

        // Проверяем, был ли уже день рождения в этом году
        if (dateOfBirth.Date > today.AddYears(-age))
        {
            age--;
        }

        if (age < 18)
        {
            return (false, "Вы должны быть старше 18 лет", null);
        }

        var user = new User
        {
            Email = email,
            Password = password, // В реальном приложении нужно хеширование
            FullName = fullName,
            DateOfBirth = dateOfBirth,
            RegisteredAt = DateTime.Now,
            Balance = 0m,
            IsEmailVerified = false,
            VerificationCode = new Random().Next(1000, 9999).ToString()
            // Не присваиваем IsAgeVerified, так как это свойство только для чтения
            // Предполагаем, что оно вычисляется автоматически из DateOfBirth
        };

        _users.Add(user);
        _notificationService?.NotifyRegistration(user.Id, user.FullName);

        // Отправка кода
        var (emailSuccess, emailError) = await _emailService.SendVerificationCodeAsync(email, user.VerificationCode, fullName);
        _verificationCodes.Add(new VerificationCodeEntry { Email = email, Code = user.VerificationCode!, CreatedAt = DateTime.Now });

        if (!emailSuccess)
        {
            return (true, $"Пользователь создан, но письмо не отправлено: {emailError}", user);
        }

        return (true, $"Код подтверждения отправлен на {email}", user);
    }

    // Legacy sync method for backward compatibility if needed, but better to use Async everywhere
    public (bool success, string message, User? user) Register(string email, string password, string fullName, DateTime dateOfBirth)
    {
        return RegisterAsync(email, password, fullName, dateOfBirth).GetAwaiter().GetResult();
    }

    public (bool success, string message) VerifyEmail(string email, string code)
    {
        email = email.Trim().ToLower();
        code = code.Trim();
        var user = _users
            .Where(u => u.Email == email)
            .OrderByDescending(u => u.RegisteredAt)
            .FirstOrDefault(u => string.Equals(u.VerificationCode, code, StringComparison.Ordinal));

        if (user == null)
            return (false, "Неверный код подтверждения");

        if (user.IsEmailVerified)
            return (true, "Email уже подтвержден");

        if (user.VerificationCode == code)
        {
            user.IsEmailVerified = true;
            user.VerificationCode = null; // Сброс кода
            CurrentUser = user; // Автоматический вход после подтверждения
            EnsureCurrentUserInList();
            OnAuthStateChanged?.Invoke();
            return (true, "Email успешно подтвержден!");
        }

        return (false, "Неверный код подтверждения");
    }

    /// <summary>
    /// Повторно отправить код. Возвращает код или сообщение об ошибке (через exception или спец. формат)
    /// </summary>
    public async Task<(string? code, string message)> ResendCodeAsync(string email)
    {
        email = email.Trim().ToLower();
        var user = _users
            .Where(u => u.Email == email)
            .OrderBy(u => u.IsEmailVerified)
            .ThenByDescending(u => u.RegisteredAt)
            .FirstOrDefault();
        if (user == null) return (null, "Пользователь не найден");

        user.VerificationCode = new Random().Next(1000, 9999).ToString();
        var (success, error) = await _emailService.SendVerificationCodeAsync(email, user.VerificationCode, user.FullName);
        _verificationCodes.Add(new VerificationCodeEntry { Email = email, Code = user.VerificationCode!, CreatedAt = DateTime.Now });

        if (!success)
        {
            return (user.VerificationCode, $"Ошибка отправки: {error}. Код для теста: {user.VerificationCode}");
        }

        return (user.VerificationCode, "Код отправлен повторно");
    }

    public string ResendCode(string email)
    {
        var result = ResendCodeAsync(email).GetAwaiter().GetResult();
        return result.code ?? string.Empty;
    }

    /// <summary>
    /// Вход пользователя
    /// </summary>
    public (bool success, string message, User? user) Login(string email, string password)
    {
        email = email.Trim().ToLower();
        password = password.Trim();
        var user = _users.FirstOrDefault(u => u.Email == email && u.Password == password);

        if (user == null)
            return (_users.Any(u => u.Email == email)
                ? (false, "Неверный пароль", null)
                : (false, "Пользователь не найден", null));

        if (!user.IsEmailVerified)
            return (false, "Email не подтвержден", user);

        user.LastLogin = DateTime.Now;
        CurrentUser = user;
        EnsureCurrentUserInList();
        OnAuthStateChanged?.Invoke();
        return (true, "Вход успешен", user);
    }

    /// <summary>
    /// Выйти из аккаунта
    /// </summary>
    public void Logout()
    {
        CurrentUser = null;
        OnAuthStateChanged?.Invoke();
    }

    public void NotifyStateChanged()
    {
        OnAuthStateChanged?.Invoke();
    }

    /// <summary>
    /// Попросить открыть модалку авторизации в UI
    /// </summary>
    public string PreferredAuthMode { get; private set; } = "login";
    public string LastAuthRequest { get; private set; } = string.Empty;

    public void RequestLogin(string mode = "login")
    {
        PreferredAuthMode = mode;
        LastAuthRequest = $"RequestLogin: {mode} | currentUser: {(CurrentUser?.Email ?? "null")} | {DateTime.Now:HH:mm:ss}";
        OnDebugStateChanged?.Invoke();
        OnOpenAuthRequested?.Invoke();
    }

    /// <summary>
    /// Получить пользователя по ID
    /// </summary>
    public User? GetUserById(string id)
    {
        EnsureCurrentUserInList();
        return _users.FirstOrDefault(u => u.Id == id);
    }

    /// <summary>
    /// Получить пользователя по email
    /// </summary>
    public User? GetUserByEmail(string email)
    {
        return _users.FirstOrDefault(u => u.Email == email);
    }

    private void EnsureCurrentUserInList()
    {
        if (CurrentUser == null)
            return;
        if (_users.Any(u => u.Id == CurrentUser.Id))
            return;
        _users.Add(CurrentUser);
    }

    /// <summary>
    /// Переключить статус избранного для аукциона
    /// </summary>
    public void ToggleFavorite(string auctionId)
    {
        if (CurrentUser == null)
        {
            RequestLogin();
            return;
        }

        if (CurrentUser.FavoriteAuctionIds.Contains(auctionId))
        {
            CurrentUser.FavoriteAuctionIds.Remove(auctionId);
        }
        else
        {
            CurrentUser.FavoriteAuctionIds.Add(auctionId);
        }
        OnAuthStateChanged?.Invoke();
    }

    /// <summary>
    /// Проверить, находится ли аукцион в избранном
    /// </summary>
    public bool IsFavorite(string auctionId)
    {
        return CurrentUser?.FavoriteAuctionIds.Contains(auctionId) ?? false;
    }

    /// <summary>
    /// Получить всех пользователей
    /// </summary>
    public List<User> GetAllUsers()
    {
        return _users;
    }

    /// <summary>
    /// Привязать карту
    /// </summary>
    public bool LinkCard(string userId, string cardNumber)
    {
        var user = GetUserById(userId);
        if (user == null)
            return false;

        // Очистить номер карты от пробелов и дефисов
        var cleanCardNumber = cardNumber.Replace(" ", "").Replace("-", "");

        // Проверка формата (только цифры, минимум 13, максимум 19)
        if (cleanCardNumber.Length < 13 || cleanCardNumber.Length > 19 || !cleanCardNumber.All(char.IsDigit))
            return false;

        // Извлечь последние 4 цифры
        var mask = cleanCardNumber.Length >= 4 ? "**** **** **** " + cleanCardNumber.Substring(cleanCardNumber.Length - 4) : "****";

        user.IsCardLinked = true;
        user.CardMask = mask;
        OnAuthStateChanged?.Invoke(); // Уведомить об изменении
        return true;
    }

    /// <summary>
    /// Пополнить баланс
    /// </summary>
    public (bool success, string message) AddBalance(string userId, decimal amount)
    {
        var user = GetUserById(userId);
        if (user == null)
            return (false, "Пользователь не найден");

        if (!user.IsCardLinked)
            return (false, "Привяжите карту перед пополнением");

        if (amount <= 0)
            return (false, "Сумма должна быть больше нуля");

        user.Balance += amount;
        _balanceTransactions.Add(new BalanceTransaction
        {
            UserId = userId,
            Amount = amount,
            Type = BalanceTransactionType.Deposit,
            Description = "Пополнение баланса",
            CreatedAt = DateTime.Now
        });

        // Уведомление на почту и в систему
        try
        {
            var subject = "Пополнение баланса";
            var message = $"Ваш баланс успешно пополнен на {amount:N0} грошей.";
            // template_il603yh - шаблон для пополнения (как просил пользователь)
            _emailService.SendEmailAsync(user.Email, subject, message, user.FullName, "template_il603yh").GetAwaiter().GetResult();
            _notificationService?.CreateSystemNotification(userId, "Баланс пополнен", $"На ваш счет зачислено {amount:N0} грошей");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending topup notification: {ex.Message}");
        }

        OnAuthStateChanged?.Invoke(); // Уведомить об изменении баланса
        return (true, $"Баланс пополнен на {amount:N0} гроши");
    }

    public List<VerificationCodeEntry> GetAllVerificationCodes() => _verificationCodes;
    public List<BalanceTransaction> GetAllBalanceTransactions() => _balanceTransactions;

    public void RecordTransaction(string userId, decimal amount, BalanceTransactionType type, string? relatedAuctionId = null, string? description = null)
    {
        _balanceTransactions.Add(new BalanceTransaction
        {
            UserId = userId,
            Amount = amount,
            Type = type,
            RelatedAuctionId = relatedAuctionId,
            Description = description,
            CreatedAt = DateTime.Now
        });
    }

    /// <summary>
    /// Вывести средства
    /// </summary>
    public (bool success, string message) WithdrawBalance(string userId, decimal amount)
    {
        var user = GetUserById(userId);
        if (user == null)
            return (false, "Пользователь не найден");

        if (user.AvailableBalance < amount)
            return (false, "Недостаточно средств");

        user.Balance -= amount;
        return (true, $"Выведено {amount} гроши");
    }

    /// <summary>
    /// Подтвердить email
    /// </summary>
    public bool VerifyEmail(string userId)
    {
        var user = GetUserById(userId);
        if (user == null)
            return false;

        user.IsEmailVerified = true;
        return true;
    }

    /// <summary>
    /// Обновить профиль
    /// </summary>
    public (bool success, string message) UpdateProfile(
        string userId,
        string fullName,
        bool receiveNotifications)
    {
        var user = GetUserById(userId);
        if (user == null)
            return (false, "Пользователь не найден");

        user.FullName = fullName;
        user.ReceiveNotifications = receiveNotifications;

        return (true, "Профиль обновлен");
    }

    /// <summary>
    /// Обновить рейтинг пользователя
    /// </summary>
    public void UpdateRating(string userId, decimal newRating)
    {
        var user = GetUserById(userId);
        if (user != null)
        {
            user.Rating = newRating;
        }
    }

    /// <summary>
    /// Установить ограничение пользователю
    /// </summary>
    public void RestrictUser(string userId, string message)
    {
        var user = GetUserById(userId);
        if (user != null)
        {
            user.RestrictionMessage = message;
            user.LastRestrictionDate = DateTime.Now;
            _notificationService?.CreateSystemNotification(userId, "Ограничение от администрации", message);
        }
    }
}
