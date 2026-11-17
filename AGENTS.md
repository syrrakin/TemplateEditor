# AGENTS.md — правила работы агента в этом проекте

## Технологический стек

- **Язык:** C# 12
- **Платформа:** .NET 8.0 (LTS)
- **Тип приложения:** десктопное приложение **WPF** (`net8.0-windows`, `UseWPF=true`)
- **Архитектура:** MVVM на базе **CommunityToolkit.Mvvm** (см. раздел «WPF и MVVM»)
- **Сборка:** `dotnet build`
- **Пакетный менеджер:** NuGet (`dotnet add package`), только через `PackageReference` в `.csproj` — без `packages.config`

## Структура проекта

```
├── src/                       # исходный код
│   └── <ProjectName>/         # основной проект WPF (.csproj)
│       ├── Views/             # окна и страницы (XAML + code-behind, только UI-логика)
│       ├── ViewModels/        # ViewModel'ы (наследуются от ObservableObject)
│       ├── Models/            # модели предметной области
│       ├── Services/          # сервисы (доступ к данным, файлам, API)
│       ├── Converters/        # IValueConverter для XAML
│       ├── Resources/         # стили, шаблоны, словари ресурсов
│       ├── App.xaml           # точка входа, регистрация ресурсов
│       └── appsettings.json   # конфигурация (если используется)
├── tests/                     # тесты
│   └── <ProjectName>.Tests/   # тесты ViewModel и сервисов
├── docs/                      # документация
├── .editorconfig              # единые правила форматирования
├── .gitignore
└── <SolutionName>.sln         # файл решения в корне
```

Новый код — только в `src/`, новые тесты — только в `tests/`. Файлы вне этих папок (конфиги, CI, скрипты) изменять только по явному запросу.

## Команды

```bash
# Сборка
dotnet build

# Запуск тестов (обязательно после любых изменений кода)
dotnet test

# Запуск приложения
dotnet run --project src/<ProjectName>

# Проверка форматирования
dotnet format --verify-no-changes

# Восстановление зависимостей
dotnet restore
```

**Перед тем как сообщить, что задача выполнена:** сборка должна проходить без ошибок и предупреждений уровня `Warning`, а `dotnet test` — зелёным.

## Соглашения по коду

- Nullable reference types включены (`<Nullable>enable</Nullable>`) — не отключать, предупреждения о `null` исправлять, а не подавлять.
- Использовать современные возможности C# 12: primary constructors (где уместно), `required` members, collection expressions.
- Асинхронность: `async`/`await` без `.Result` и `.Wait()`; методы с суффиксом `Async`, принимают `CancellationToken` там, где операция может быть отменена.
- Именование: `PascalCase` — классы, методы, свойства; `camelCase` — параметры и локальные переменные; `_camelCase` — приватные поля.
- Один публичный тип — один файл, имя файла совпадает с именем типа.
- Комментарии на английском; публичный API документировать XML-комментариями (`///`).
- Не использовать `#region`.

## WPF и MVVM (CommunityToolkit.Mvvm)

- **MVVM-фреймворк:** `CommunityToolkit.Mvvm` — единственный источник MVVM-инфраструктуры. Рукописные реализации `INotifyPropertyChanged`, `RelayCommand`, `ViewModelBase` не создавать и не использовать.
- **ViewModel'ы** наследуются от `ObservableObject` (или `ObservableRecipient`, если нужен `IMessenger`) и объявляются как `partial`.
- **Свойства** — через source generator: приватное поле с атрибутом `[ObservableProperty]` (например, `[ObservableProperty] private string _userName;` → свойство `UserName`). Атрибуты `[NotifyPropertyChangedFor]` и `[NotifyCanExecuteChangedFor]` применять при зависимостях между свойствами/командами.
- **Команды** — через `[RelayCommand]` над методом; для асинхронных операций — `async Task`-метод с атрибутом `[RelayCommand]`, поддерживающий отмену через `CancellationToken`. Вручную `ICommand` не реализовывать.
- **Обмен сообщениями** между ViewModel'ами — через `IMessenger` / `WeakReferenceMessenger` из CommunityToolkit, а не через события или прямые ссылки.
- **Валидация** — через `ObservableValidator` и атрибуты `System.ComponentModel.DataAnnotations`, где требуется проверка ввода.
- **Code-behind** (`*.xaml.cs`) — только чисто визуальная логика (анимации, фокус, обработчики, специфичные для контролов). Бизнес-логика и доступ к данным в code-behind запрещены.
- **XAML:** привязки через `x:Bind`-эквивалент не поддерживается — использовать `Binding` с `x:DataType` не требуется, но DataContext типизировать через `d:DataContext` (design-time) для подсветки ошибок привязок. Общие стили и шаблоны — в `Resources/`, а не в разметке окон.
- **UI-поток:** длительные операции — в фоне (`Task.Run`, async-сервисы); обновление привязанных коллекций — только в UI-потоке (`ObservableCollection<T>` не трогать из фоновых потоков без `Dispatcher`).
- **DI:** при необходимости — `Microsoft.Extensions.DependencyInjection` + `Microsoft.Extensions.Hosting`; регистрация сервисов и ViewModel'ов в `App.xaml.cs`.

## Зависимости

- Стандартные для проекта пакеты: `CommunityToolkit.Mvvm` (обязателен), при необходимости — `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Configuration*`. Их добавлять без дополнительного согласования.
- Новые NuGet-пакеты сверх этого списка добавлять только при реальной необходимости; сначала искать решение в стандартной библиотеке .NET 8.
- Перед добавлением нестандартного пакета или пакета с большим деревом зависимостей — уточнить у пользователя.
- Версии пакетов фиксировать явно (при желании — через `Directory.Packages.props`).

## Тестирование

- Фреймворк: **xUnit** (при наличии другого — следовать существующему в проекте).
- Тесты пишутся для ViewModel'ов и сервисов; View (XAML) не тестируются.
- ViewModel'ы тестируются как обычные классы: изменение свойства → проверка `PropertyChanged`, выполнение команды → проверка побочных эффектов на замоканных сервисах.
- Именование тестов: `MethodName_StateUnderTest_ExpectedBehavior`.
- Один тест — одна логическая проверка; без хрупких зависимостей от порядка выполнения.
- Моки: **Moq** или **NSubstitute** — в соответствии с уже используемым в проекте.

## Безопасность и ограничения

- Секреты (строки подключения, ключи, пароли) — только через user-secrets / переменные окружения / `appsettings.Development.json`, никогда не коммитить и не выводить в чат.
- `appsettings.json` изменять аккуратно; реальные значения окружений не трогать.
- Деструктивные операции (`git reset --hard`, `force-push`, удаление файлов, миграции БД с потерей данных) — только после явного подтверждения пользователя.
- Не менять целевую платформу (`TargetFramework`) без отдельного запроса.

## Git

- Коммиты атомарные, сообщения в стиле Conventional Commits: `feat:`, `fix:`, `refactor:`, `test:`, `chore:`.
- Коммитить и пушить только по явной просьбе пользователя.
