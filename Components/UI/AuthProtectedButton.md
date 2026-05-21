# AuthProtectedButton Component

Компонент кнопки с встроенной защитой аутентификации. Автоматически перенаправляет гостей на форму входа.

## Использование

### Базовое использование
```razor
<AuthProtectedButton CssClass="btn btn-primary"
                    AuthMode="login"
                    GuestModeText="Войти для участия"
                    AuthenticatedText="Сделать ставку"
                    OnClick="HandleBid">
    Сделать ставку
</AuthProtectedButton>
```

### Параметры

- **CssClass** - CSS классы для стилизации кнопки
- **AuthMode** - режим авторизации ("login" или "register") 
- **GuestModeText** - текст для гостевого режима
- **AuthenticatedText** - текст для авторизованных пользователей (опционально)
- **RequireAuthentication** - требуется ли аутентификация (по умолчанию true)
- **Disabled** - отключена ли кнопка
- **ButtonType** - тип кнопки ("button", "submit", "reset")
- **OnClick** - обработчик клика для авторизованных пользователей

## Примеры

### Кнопка ставки в аукционе
```razor
<AuthProtectedButton CssClass="w-full py-4 rounded-2xl font-bold text-sm text-white"
                    style="background: linear-gradient(135deg, #ef4444 0%, #f97316 100%)"
                    AuthMode="login"
                    GuestModeText="Войти для участия"
                    AuthenticatedText="Сделать ставку"
                    OnClick="PlaceBid">
    Сделать ставку
</AuthProtectedButton>
```

### Кнопка добавления в избранное
```razor
<AuthProtectedButton CssClass="w-10 h-10 rounded-full bg-black/50 text-white"
                    AuthMode="login"
                    GuestModeText="❤"
                    AuthenticatedText="❤️"
                    OnClick="ToggleFavorite">
    ❤
</AuthProtectedButton>
```

### Кнопка без защиты (доступна всем)
```razor
<AuthProtectedButton CssClass="btn btn-secondary"
                    RequireAuthentication="false"
                    OnClick="ViewAuctions">
    Смотреть аукционы
</AuthProtectedButton>
```

## Как это работает

1. **Для гостей**: Кнопка автоматически показывает `GuestModeText` и при клике вызывает `UserService.RequestLogin(AuthMode)`
2. **Для авторизованных**: Кнопка показывает `AuthenticatedText` (или исходный контент) и выполняет `OnClick` обработчик
3. **Перенаправление**: `UserService.RequestLogin()` вызывает событие `OnOpenAuthRequested`, которое открывает модальное окно авторизации в `MainLayout`

## Преимущества

- ✅ Единообразная обработка гостевого режима
- ✅ Автоматическое перенаправление на вход
- ✅ Поддержка разных режимов авторизации
- ✅ Гибкая стилизация
- ✅ Обратная совместимость