# Збірка на Linux

## Стан портування

| Проєкт | Таргет | Windows | Linux / macOS |
|---|---|---|---|
| `CL.Core` | `net8.0` | ✅ | ✅ |
| `CL.Core.Tests` | `net8.0` | ✅ | ✅ |
| `CL.Avalonia` | `net8.0` | ✅ | ✅ |
| `CL(CLegendary Launcher)` | `net8.0-windows` | ✅ | ❌ WPF |

`CL.Core` - спільне ядро (моделі, сервіси, робота з мережею та файлами). Обидва
UI підключають його як `ProjectReference`, тому логіка не дублюється.

`CL.Avalonia` - кросплатформенний інтерфейс. Наразі це каркас, але він уже
**запускає гру**: обери версію зі списку, нікнейм і Java, далі "Грати".
Перевірено на Linux з Minecraft 1.20.4 (Java 25). Перенесення решти екранів
з WPF - робота, що триває.

WPF-проєкт **не збирається** ніде, крім Windows: `dotnet build` на Linux падає
з внутрішньою помилкою CLR, тому для крос-платформенної роботи є окремий
solution без нього.

## Залежності

- .NET SDK 8.0 або новіший
- Для запуску гри - Java (пошук автоматичний, див. нижче)

```bash
# Debian / Ubuntu
sudo apt install dotnet-sdk-8.0 openjdk-21-jre

# Fedora
sudo dnf install dotnet-sdk-8.0 java-21-openjdk

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

Якщо встановлено лише новіший рантайм (наприклад, .NET 9), а таргет - `net8.0`:

```bash
DOTNET_ROLL_FORWARD=Major dotnet run --project CL.Avalonia
```

## Де лежать дані

| ОС | Каталог |
|---|---|
| Windows | `%APPDATA%\.ClMinecraft` |
| Linux | `~/.clminecraft` |
| macOS | `~/Library/Application Support/CLMinecraft` |

Шлях повертає `PlatformPaths.DefaultLauncherPath()`.

## Пошук Java

`JavaLocator.Detect()` перевіряє кожного кандидата запуском `java -version`,
тому у списку лише робочі рантайми з відомою мажорною версією. Симлінки
розгортаються, тому `java-21-openjdk` і `java-1.21.0-openjdk` не дублюються.

Що саме проглядається:

- `PATH`, `JAVA_HOME`, `JDK_HOME`
- `runtime/` у каталозі лаунчера (рантайми, завантажені Mojang)
- Linux: `/usr/lib/jvm`, `/usr/lib64/jvm`, `/usr/java`, `/opt/java`,
  `~/.sdkman/candidates/java`, `~/.jdks`
- macOS: `/Library/Java/JavaVirtualMachines` (разом з `Contents/Home`)
- Windows: `Program Files/Java`, `Eclipse Adoptium`, `Microsoft`,
  `AdoptOpenJDK`, `BellSoft`

## Що ще залишилось для повної підтримки Linux

Перенести на `CL.Core` або на абстракцію те, що зараз прив'язане до Windows:

- **Файлові діалоги** - `Microsoft.Win32.OpenFileDialog` та
  `System.Windows.Forms.FolderBrowserDialog` (потрібен спільний інтерфейс,
  реалізації WPF і Avalonia `IStorageProvider`)
- **Запуск гри**: базовий (ваніль, офлайн-акаунт) уже працює через
  `MinecraftLaunchService` у ядрі. Модлоадери, акаунти Microsoft/LittleSkin,
  прогрес завантаження та модпаки лишились у `GameLaunchService` і
  `ModpackService`, які тягнуть WPF
- **Звук** - `SoundManager` на `System.Media.SoundPlayer` (лише Windows)
- **Екрани** - 37 `.xaml` та 56 code-behind ще на WPF

Останнім кроком WPF-проєкт стає лише Windows-обгорткою над спільним ядром.
