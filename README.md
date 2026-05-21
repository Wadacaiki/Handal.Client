# Handal - Premium Auction Platform

Полнофункциональная платформа премиум-аукционов, реализованная на **ASP.NET Core Blazor** с современным дизайном и интерактивностью.

## 🏗 Архитектура проекта

Проект разделен на следующие компоненты:

### Структура папок
```
📁 Handal.Client
│
├── 📁 Connected Services
│
├── 📁 Properties
│
├── 📁 wwwroot
│   ├── 📁 css
│   │   ├── components.css
│   │   ├── fonts.css
│   │   ├── forms.css
│   │   ├── index.css
│   │   ├── layout.css
│   │   ├── tailwind.css
│   │   ├── theme.css
│   │   ├── utilities.css
│   │   └── variables.css
│   ├── 📁 images
│   └── index.html
│
├── 📁 Зависимости (Dependencies)
│
├── 📁 Components
│   └── 📁 UI
│       ├── AuctionCard.razor
│       ├── AuctionCardWithServices.razor
│       └── Chat.razor
│
├── 📁 Models
│   ├── Auction.cs
│   ├── AuctionHistory.cs
│   ├── BalanceTransaction.cs
│   ├── Bid.cs
│   ├── ChatMessage.cs
│   ├── Notification.cs
│   └── Tagging.cs
│
├── 📁 Pages
│   ├── Admin.razor
│   ├── AdminTags.razor
│   ├── AppraisalResponse.razor
│   ├── AuctionDetail.razor
│   ├── History.razor
│   ├── Index.razor
│   ├── Index.razor.css
│   ├── Notifications.razor
│   └── Profile.razor
│
├── 📁 Services
│   ├── AuctionPlatformService.cs
│   ├── AuctionService.cs
│   ├── BidService.cs
│   ├── ChatService.cs
│   ├── EmailJsService.cs
│   ├── IEmailService.cs
│   ├── NotificationService.cs
│   ├── PersistenceService.cs
│   ├── TagService.cs
│   └── UserService.cs
│
├── 📁 Shared
│   ├── Categories.razor
│   ├── Header.razor
│   ├── Hero.razor
│   └── MainLayout.razor
│
├── .gitattributes
├── .gitignore
├── _Imports.razor
├── App.razor
├── package.json
├── package-lock.json
├── postcss.config.js
└── Program.cs
```

## 🚀 Начало работы

### Требования
- .NET 8.0+
- Node.js 18+ (для Tailwind CSS компиляции)
- Visual Studio 2022 или VS Code

### Установка и запуск

1. **Установить зависимости**
```bash
cd src/Handal.Client
npm install
```

2. **Запустить разработку Tailwind CSS** (опционально, в отдельном терминале)
```bash
npm run dev:css
```

3. **Запустить Blazor приложение**
```bash
dotnet run --project src/Handal.Client/Handal.Client.csproj
```

Приложение будет доступно по адресу `https://localhost:61313`

## 📦 Компоненты

### Основные Blazor компоненты

| Компонент | Описание |
|-----------|---------|
| `AuctionCard.razor` | Карточка лота с информацией о ставках |
| `Header.razor` | Навигационная панель |
| `Hero.razor` | Главный баннер |
| `Categories.razor` | Категории товаров |
| `Card.razor` | Базовый компонент карточки |
| `Input.razor` | Кастомный input компонент |
| `Label.razor` | Компонент лабеля |

### Стили и темы

- **Tailwind CSS 4.1** для стилизации
- **Кастомная цветовая схема**:
  - Основной цвет: `#682021` (красно-коричневый)
  - Акцент: `#EFD867` (золотой)
  - Фон: `#0C0C0C` (почти чёрный)

## 🎯 Функциональность

### Реализованные возможности
- ✅ Просмотр лотов с фильтрацией по категориям
- ✅ Интерактивные карточки аукционов
- ✅ Система аутентификации (вход/регистрация)
- ✅ Размещение ставок в реальном времени
- ✅ Отслеживание баланса пользователя
- ✅ Адаптивный дизайн для всех экранов

### План развития
- [ ] Интеграция с backend API
- [ ] Система уведомлений
- [ ] Сохранение избранных лотов
- [ ] История ставок пользователя
- [ ] Push-уведомления

## 🛠 Технологический стек

### Frontend
- **Blazor WebAssembly** - интерактивные UI компоненты
- **Razor Components** - компонентная архитектура
- **Tailwind CSS 4.1** - утилит-первый CSS фреймворк
- **C# 12** - язык программирования

### Build инструменты
- **Vite** (удален) - ❌ больше не используется
- **React** (удален) - ❌ полностью заменен на Blazor
- **Node.js npm** - управление зависимостями CSS

## 📝 Миграция с React на Blazor

Проект был полностью перенесен с React на ASP.NET Core Blazor:

### Преимущества Blazor
- Один язык (C#) для frontend и backend
- Лучшая интеграция с .NET экосистемой
- WebAssembly для выполнения в браузере
- Встроенная валидация и data binding
- Меньше JavaScript, больше типизации

## 🎨 Кастомизация

### Изменение цветов
Отредактируйте значения переменных в стилях:
- Основной цвет: Измените `#682021` на нужный
- Акцент: Измените `#EFD867` на нужный
- Фон: Измените `#0C0C0C` на нужный

### Добавление новых компонентов
1. Создайте файл `NewComponent.razor` в папке `Components/`
2. Наследуйтесь от `ComponentBase`
3. Используйте `@code { }` блок для логики

## 📄 Лицензия

Проект основан на Figma дизайне. Все права защищены.

## 👤 Автор

Разработано как полнофункциональное Blazor приложение для премиум-аукционов.

---

**Статус**: ✅ Проект полностью перенесен с React на Blazor WebAssembly
