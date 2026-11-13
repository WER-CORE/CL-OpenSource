<div align="center">

# Clegendary Launcher

**Відкритий Minecraft-лаунчер, який розвивається в Україні.**

[![Release](https://img.shields.io/github/v/release/WER-CORE/CL-Win-Edition--Update?label=Release)](https://github.com/WER-CORE/CL-Win-Edition--Update/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/WER-CORE/CL-Win-Edition--Update/total?label=Downloads)](https://github.com/WER-CORE/CL-Win-Edition--Update/releases)
![Platforms](https://img.shields.io/badge/Platforms-Windows%20%7C%20Linux%20%7C%20macOS-555)
![Made in Ukraine](https://img.shields.io/badge/Made%20in-Ukraine-0057b7)

[**Завантажити**](https://github.com/WER-CORE/CL-Win-Edition--Update/releases/latest) · [Повідомити про проблему](https://github.com/WER-CORE/CL-OpenSource/issues) · [Discord](https://discord.gg/RhyZjACs2U) · [Допомогти з перекладом](https://uk.crowdin.com/project/clegendary-launcher)

**Мова:** Українська · [English, Polski, Беларуская, Čeština, Français — долучитися до перекладу](https://uk.crowdin.com/project/clegendary-launcher)

</div>

---

## Про проєкт

**Clegendary Launcher (CL)** — відкритий лаунчер для Minecraft, який об'єднує керування версіями гри, модифікаціями та збірками в одному застосунку.

Ми розвиваємо український програмний продукт і хочемо, щоб у гравців була якісна альтернатива закритим та небажаним лаунчерам. Проєкт відкритий до внесків, зауважень і пропозицій спільноти.

> **Свій до свого по своє.** 🇺🇦

## Можливості

- **Версії Minecraft.** Встановлення та запуск підтримуваних версій гри.
- **Історичні версії.** Pre-Classic, Classic, Indev, Infdev, Alpha та Beta — завдяки інтеграції з [BetaCraft](https://betacraft.uk/). Доступність і коректність запуску окремих збірок залежить від Java, операційної системи та сумісності старих компонентів.
- **Моди та контент.** Пошук і завантаження через [Modrinth](https://modrinth.com/) і [CurseForge](https://www.curseforge.com/minecraft).
- **Завантажувачі модів.** Підтримка Forge, Fabric, Quilt, NeoForge, OptiFine та LiteLoader у відповідних сценаріях і версіях.
- **Облікові записи.** Підтримувані способи авторизації, зокрема Microsoft і LittleSkin.
- **Збірки Minecraft.** Інструменти для роботи з модпаками та їхнім вмістом.
- **Налаштування.** Параметри запуску та інтерфейсу лаунчера.

> **Примітка про сумісність:** не кожна можливість доступна для кожної версії Minecraft або на кожній платформі. Особливо це стосується історичних версій і старих завантажувачів модів.

## Скриншоти

<div align="center">

<img width="750" alt="Інтерфейс Clegendary Launcher — скриншот 1" src="https://github.com/user-attachments/assets/4f4696dd-031d-4d5f-b564-2b5225a62ace" />

<img width="750" alt="Інтерфейс Clegendary Launcher — скриншот 2" src="https://github.com/user-attachments/assets/fe144e5a-4ade-4e12-9dab-f16649d1ccde" />

<sub>Скриншоти попередньої версії інтерфейсу. Актуальний вигляд може відрізнятися.</sub>

</div>

## Платформи та технології

Лаунчер розробляється на **C# / .NET**. Актуальний кросплатформений інтерфейс використовує **Avalonia UI**; у проєкті також є історичний код на WPF.

| Компонент | Технологія |
| --- | --- |
| Мова | C# |
| Платформа | .NET 8 / .NET 10 (залежно від проєкту та конфігурації) |
| Інтерфейс | Avalonia UI |
| Запуск Minecraft | CmlLib.Core |
| Каталоги модів | Modrinth API, CurseForge API |
| Історичні версії | BetaCraft — каталог, ресурси й інструменти сумісності |

**Цільові платформи:** Windows, Linux і macOS. Наявність збірки для платформи не гарантує, що всі функції пройшли повне ручне тестування на ній.

## Завантаження

Готові випуски та інформацію про їхню доступність шукайте на [сторінці релізів](https://github.com/WER-CORE/CL-Win-Edition--Update/releases/latest).

Якщо помітили помилку, створіть [issue](https://github.com/WER-CORE/CL-OpenSource/issues) і, за можливості, додайте версію лаунчера, ОС, кроки відтворення та лог без особистих даних.

## Збірка з вихідного коду

**Вимоги:** Git, сумісний .NET SDK (8 і/або 10 — відповідно до цільового проєкту), IDE на вибір: Visual Studio, JetBrains Rider або інший редактор з підтримкою .NET.

```bash
git clone https://github.com/WER-CORE/CL-OpenSource.git
cd CL-OpenSource
dotnet restore CL.Avalonia/CL.Avalonia.csproj
dotnet build CL.Avalonia/CL.Avalonia.csproj -c Release
```

Для перевірки основної бібліотеки:

```bash
dotnet build CL.Core/CL.Core.csproj -c Release
```

Структура рішення та необхідні SDK можуть змінюватися між гілками. Якщо збірка не проходить, перевірте повідомлення `dotnet` і цільові фреймворки у файлах `.csproj`.

## Участь у розробці

Ми вітаємо повідомлення про помилки, пропозиції, виправлення та переклади. Перед великими змінами бажано створити issue й обговорити задум.

- [Проблеми та пропозиції](https://github.com/WER-CORE/CL-OpenSource/issues)
- [Переклад у Crowdin](https://uk.crowdin.com/project/clegendary-launcher)
- [Спільнота Discord](https://discord.gg/RhyZjACs2U)

## Подяки спільноті та стороннім проєктам

Clegendary Launcher використовує сторонні бібліотеки, сервіси та ресурси. Ми вдячні їхнім авторам і спільнотам.

- **BetaCraft** — особлива подяка [команді BetaCraft](https://betacraft.uk/) ([GitHub](https://github.com/betacraftuk)) за збереження історичних версій Minecraft, створення архіву та інструментів, які допомагають запускати ранні збірки гри. Саме завдяки інтеграції з BetaCraft у Clegendary Launcher з'явилася підтримка історичних версій. Clegendary Launcher — незалежний проєкт і не є офіційним продуктом BetaCraft.
- **Modrinth і CurseForge** — платформи поширення модифікацій і контенту Minecraft.
- **Forge, Fabric, Quilt, NeoForge, OptiFine і LiteLoader** — екосистема завантажувачів і модифікацій.
- **LittleSkin** — сервіс, інтегрований у підтримувані сценарії авторизації.
- **Google Fonts, Material Icons і Fluent System Icons** — джерела іконографіки та графічних ресурсів.
- **SignPath** — подяка за програму безкоштовного підписування коду, якщо вона застосовується до відповідного випуску: [SignPath.io](https://signpath.io) та [SignPath Foundation](https://signpath.org).

Ліцензійні умови окремих компонентів визначаються їхніми власними ліцензіями. Додаткові відомості дивіться у файлах [`LICENSE`](LICENSE), [`PRIVACY.md`](PRIVACY.md) та `CREDITS` (за наявності).

## Правова інформація

Clegendary Launcher не пов'язаний із Mojang Studios або Microsoft і не схвалений ними. Minecraft та пов'язані торговельні марки належать відповідним правовласникам.

---

<div align="center">

**Підтримайте розвиток українського open-source — залиште ⭐ репозиторію, якщо проєкт вам корисний.**

</div>
