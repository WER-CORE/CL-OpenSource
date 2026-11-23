# CL.Benchmarks

Універсальна консольна утиліта (.NET 10) для порівняння продуктивності **будь-яких двох збірок CL** (`Baseline` vs `Candidate`, наприклад `1.1.3` vs `1.1.4`, `1.1.4` vs `1.1.5` тощо).

## Можливості

- **Сценарії:**
  - `startup` — заміри старту процесу, часу до першого валідного семпла пам'яті (`TimeToFirstValidSampleMs`), початкового CPU та RAM.
  - `idle` — спостереження за стабільним простоєм протягом заданого часу (`--duration`).
  - `navigation` — інтерактивний керований режим для ручного перемикання екранів у лаунчері із замірами CPU/RAM (`WorkingSet` vs `PrivateMemory`).
- **Чергування запусків:** парні запуски чергуються (`Baseline -> Candidate`, потім `Candidate -> Baseline`), щоб зменшити вплив кешування й прогріву.
- **Метрики:** `ProcessStartMs`, `TimeToFirstValidSampleMs`, `TotalProcessorTimeMs`, нормалізований `AvgCpuPercent` / `MaxCpuPercent` (відносно `Environment.ProcessorCount`), `AvgWorkingSetMb`, `PeakWorkingSetMb`, `AvgPrivateMemoryMb`, `MaxThreadCount`, стабільність (exit codes, несподіване завершення, таймаути).
- **Звіти та історія:** експорт у JSON, CSV та Markdown (`((candidate - baseline) / baseline) * 100` з безпечною обробкою нульового baseline) + індекс історії запусків (`history`).
- **Безпека процесів:** утиліта завершує **лише** ті процеси, які сама запустила.

## Приклади використання

```bash
# Довідка
dotnet run --project CL.Benchmarks -- --help

# Порівняння двох збірок (наприклад 1.1.3 та 1.1.4)
dotnet run --project CL.Benchmarks -c Release -- compare \
  --baseline "C:\builds\cl-1.1.3\CL.Avalonia.exe" \
  --candidate "C:\builds\cl-1.1.4\CL.Avalonia.exe" \
  --scenario idle \
  --runs 5 \
  --duration 15 \
  --baseline-label "1.1.3" \
  --candidate-label "1.1.4"

# Перегляд історії порівнянь
dotnet run --project CL.Benchmarks -c Release -- history --limit 10
```

## Ізоляція даних `.ClMinecraft` перед реальним тестуванням CL

Перед порівнянням реальних збірок `CL.Avalonia` обов'язково ізолюйте їхні робочі дані, щоб не зачепити ваш основний каталог `%APPDATA%\.ClMinecraft` або `~/.clminecraft`:

1. Скопіюйте кожну тестову збірку в окрему тимчасову папку (наприклад, `C:\temp\bench-1.1.3\` і `C:\temp\bench-1.1.4\`).
2. Створіть поруч із виконуваним файлом порожній маркер `.portable` (або `portable.mode`) — тоді `PlatformPaths.IsPortableRoot` перенаправить `UserSaves.json`, `ProfilesManeger.json` та інші налаштування в підпапку `Data/` поруч із бінарником.
3. У `Data/UserSaves.json` задайте `"PathLacunher"` на порожню ізольовану директорію (наприклад, `C:\temp\bench-mc-data`), інакше за замовчуванням `DefaultLauncherPath()` усе одно вкаже на `%APPDATA%\.ClMinecraft`.

## Обмеження метрик

- Час готовності внутрішнього UI-рендерингу (first-frame render) без вбудованого хука в сам лаунчер позначається через `TimeToFirstValidSampleMs` (перший ненульовий семпл `WorkingSet64`).
- Деякі метрики ОС (наприклад, `PrivateMemorySize64` або `PeakWorkingSet64`) можуть повертати `N/A` залежно від платформи та прав доступу.
