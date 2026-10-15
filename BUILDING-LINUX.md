# Збірка на Linux

## Стан портування

| Проєкт | Таргет | Windows | Linux / macOS |
| --- | --- | --- | --- |
| `CL.Core` | `net8.0;net10.0` | ✅ | ✅ |
| `CL.Core.Tests` | `net8.0;net10.0` | ✅ | ✅ |
| `CL.Avalonia` | `net10.0` | ✅ | ✅ |
| `CL(CLegendary Launcher)` | `net8.0-windows` | ✅ | ❌ WPF |

`CL.Core` - спільне ядро (моделі, сервіси, робота з мережею та файлами),[CL.Core.Tests.csproj](CL.Core.Tests/CL.Core.Tests.csproj) адаптоване під мультитаргетинг. Обидва UI підключають його як `ProjectReference`, тому логіка не дублюється, а ядро автоматично підлаштовується під потрібну версію фреймворку кінцевого клієнта.

`CL.Avalonia` - кросплатформенний інтерфейс на .NET 10 (Avalonia 12 + SukiUI, MVVM на CommunityToolkit.Mvvm, DI через `Microsoft.Extensions.DependencyInjection`). Вигляд повторює WPF-клієнт: ті самі теми з фоновими зображеннями (Темна, Світла, Океан, Ліс, Захід Сонця), іконки версій і зелена кнопка запуску. Вже є сторінки **Головна** (ваніль та OptiFine, пошук, позначка встановлених версій, "Грати"), **Акаунти** (offline, Microsoft, LittleSkin) і **Налаштування** (RAM, роздільна здатність, повний екран, офлайн-режим, консоль, теми), а також прогрес завантаження, діалоги, сповіщення та вікно журналу гри. Перевірено на Linux з Minecraft 26.2. Перенесення решти екранів з WPF - робота, що триває.

Рядки інтерфейсу беруться з `LocalizationManager` за тими самими ключами, що й у WPF (`{loc:Loc Ключ, Запасний текст}` у XAML), тож переклади спільні.

WPF-проєкт **не збирається** ніде, крім Windows: `dotnet build` на Linux падає з внутрішньою помилкою CLR, тому для крос-платформенної роботи є окремий solution без нього.

## Залежності

* .NET SDK 10.0 (для Avalonia та ядра)
* .NET SDK 8.0 (для підтримки старого WPF-клієнта, якщо потрібно)
* Для запуску гри - Java (пошук автоматичний, див. нижче)

```bash
# Debian / Ubuntu / Linux Mint
sudo apt install dotnet-sdk-10.0 openjdk-21-jre

# Fedora
sudo dnf install dotnet-sdk-10.0 java-21-openjdk

# Arch
sudo pacman -S dotnet-sdk jre-openjdk
```

## Збірка та запуск

```bash
# Тільки кросплатформенні проєкти
dotnet build CL.Crossplatform.sln
dotnet test  CL.Crossplatform.sln

dotnet run --project CL.Avalonia
```

Якщо встановлено лише новіший рантайм (наприклад, майбутній .NET 11), а таргет - `net10.0`:

```bash
DOTNET_ROLL_FORWARD=Major dotnet run --project CL.Avalonia
```

## Де лежать дані

Файли гри (версії, світи, моди):

| ОС | Каталог |
| --- | --- |
| Windows | `%APPDATA%\.ClMinecraft` |
| Linux | `~/.clminecraft` |
| macOS | `~/Library/Application Support/CLMinecraft` |

Шлях повертає `PlatformPaths.DefaultLauncherPath()`.

Дані самого лаунчера (`Data/` з налаштуваннями та акаунтами, `Locales/`):

| ОС | Каталог |
| --- | --- |
| Windows | поруч із `.exe`, як і раніше |
| Linux | `$XDG_DATA_HOME/cl-launcher` (типово `~/.local/share/cl-launcher`) |
| macOS | `~/Library/Application Support/CLLauncher` |

Шлях повертає `PlatformPaths.AppDataDirectory()`. Під час першого запуску дані, які старіші збірки клали поруч із виконуваним файлом, копіюються сюди (`PlatformPaths.MigrateLegacyAppData()`).

## Пошук Java

`JavaLocator.Detect()` перевіряє кожного кандидата запуском `java -version`, тому у списку лише робочі рантайми з відомою мажорною версією. Симлінки розгортаються, тому `java-21-openjdk` і `java-1.21.0-openjdk` не дублюються.

Що саме проглядається:

* `PATH`, `JAVA_HOME`, `JDK_HOME`
* `runtime/` у каталозі лаунчера (рантайми, завантажені Mojang)
* Linux: `/usr/lib/jvm`, `/usr/lib64/jvm`, `/usr/java`, `/opt/java`, `~/.sdkman/candidates/java`, `~/.jdks`
* macOS: `/Library/Java/JavaVirtualMachines` (разом з `Contents/Home`)
* Windows: `Program Files/Java`, `Eclipse Adoptium`, `Microsoft`, `AdoptOpenJDK`, `BellSoft`

## Що ще залишилось для повної підтримки Linux

Бізнес-логіка вже живе в `CL.Core` за інтерфейсами (`IDialogService`,
`ITaskProgressService`, `IMainWindowController`, `ISoundService`), тож
Avalonia-клієнт підставляє власні реалізації через `ServiceLocator`.

Стан перевірки на Linux:

- **Файлові діалоги** - реалізовані в Avalonia через `IStorageProvider`
  (`OpenFilePickerAsync` / `OpenFolderPickerAsync`), WinForms-фільтр
  конвертується у `FilePickerFileType`
- **Діалоги та сповіщення** - `IDialogService` і `ShowSnackbar` показують
  діалоги та тости SukiUI; повідомлення ставляться в чергу, а не губляться
- **Акаунти** - offline працює повністю. Для Microsoft на Linux
  використовується **device code flow** (`msal.DeviceCode(...)`), бо
  інтерактивне вікно MSAL розраховане на Windows; у відкритій збірці
  порожній `Secrets.CLIENT_ID_AZURE`, тому вхід там недоступний.
  LittleSkin-акаунт додається, але запуск гри з ним потребує
  `authlib-injector-1.2.8.jar` поруч із лаунчером, а його в репозиторії немає
- **Запуск гри** - ваніль та OptiFine через `GameLaunchService`: прогрес
  завантаження зі скасуванням, згортання вікна під час гри, повідомлення
  про краш і `latest.log` у вікні журналу
- **Модлоадери** (Forge / Fabric / Quilt / NeoForge) - як і у WPF, ставляться
  через збірки; екрану збірок в Avalonia ще немає
- **Модпаки, CurseForge / Modrinth, сервери, новини** - екранів ще немає
- **Тека гри** - у налаштуваннях лише показується й відкривається; перенесення
  з копіюванням файлів поки живе у WPF (`LauncherSettingsService`) і має
  переїхати в ядро
- **Журнал гри** - Minecraft пише в stdout сирий log4j-XML; його варто
  розбирати в ядрі, щоб обидва клієнти показували звичайні рядки
- **Звук** - `ISoundService` в Avalonia поки заглушка (`NullSoundService`)

Останнім кроком WPF-проєкт стає лише Windows-обгорткою над спільним ядром.
