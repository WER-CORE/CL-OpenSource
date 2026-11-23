# Збірка на Linux

## Стан портування

| Проєкт | Таргет | Windows | Linux / macOS |
| --- | --- | --- | --- |
| `CL.Core` | `net8.0;net10.0` | ✅ | ✅ |
| `CL.Core.Tests` | `net8.0;net10.0` | ✅ | ✅ |
| `CL.Avalonia` | `net10.0` | ✅ | ✅ |

> Історичний Windows-проєкт `CL(CLegendary Launcher)` на WPF (`net8.0-windows`) видалено на користь кросплатформеного `CL.Avalonia`.

`CL.Core` - спільне ядро (моделі, сервіси, робота з мережею та файлами),[CL.Core.Tests.csproj](CL.Core.Tests/CL.Core.Tests.csproj) адаптоване під мультитаргетинг. Обидва UI підключають його як `ProjectReference`, тому логіка не дублюється, а ядро автоматично підлаштовується під потрібну версію фреймворку кінцевого клієнта.

`CL.Avalonia` - кросплатформенний інтерфейс на .NET 10. Наразі це каркас, але він уже **запускає гру**: обери версію зі списку, нікнейм і Java, далі "Грати". Перевірено на Linux з Minecraft 1.20.4 (Java 25). Перенесення решти екранів з WPF - робота, що триває.

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

| ОС | Каталог |
| --- | --- |
| Windows | `%APPDATA%\.ClMinecraft` |
| Linux | `~/.clminecraft` |
| macOS | `~/Library/Application Support/CLMinecraft` |

Шлях повертає `PlatformPaths.DefaultLauncherPath()`.

## Пошук Java

`JavaLocator.Detect()` перевіряє кожного кандидата запуском `java -version`, тому у списку лише робочі рантайми з відомою мажорною версією. Симлінки розгортаються, тому `java-21-openjdk` і `java-1.21.0-openjdk` не дублюються.

Що саме проглядається:

* `PATH`, `JAVA_HOME`, `JDK_HOME`
* `runtime/` у каталозі лаунчера (рантайми, завантажені Mojang)
* Linux: `/usr/lib/jvm`, `/usr/lib64/jvm`, `/usr/java`, `/opt/java`, `~/.sdkman/candidates/java`, `~/.jdks`
* macOS: `/Library/Java/JavaVirtualMachines` (разом з `Contents/Home`)
* Windows: `Program Files/Java`, `Eclipse Adoptium`, `Microsoft`, `AdoptOpenJDK`, `BellSoft`

## Історична довідка щодо міграції з WPF

Застарілий WPF-інтерфейс повністю видалено з репозиторію; актуальний кросплатформений UI працює на `CL.Avalonia` + `CL.Core` (включаючи `IStorageProvider`, кросплатформений запуск і міграцію старих налаштувань).