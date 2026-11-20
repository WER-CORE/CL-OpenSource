# CLegendary Launcher — Flatpak (документація для розробників)

Ця директорія містить маніфест, метадані та ресурси для локальної збірки, тестування й пакування **CLegendary Launcher** у форматі Flatpak.

## Основні параметри пакета

- **Application ID:** `io.github.wer_core.cl-opensource`
- **Команда запуску:** `CL.Avalonia`
- **Runtime:** `org.freedesktop.Platform//26.08`
- **SDK:** `org.freedesktop.Sdk//26.08`
- **SDK Extension (для збірки з вихідного коду):** `org.freedesktop.Sdk.Extension.dotnet10`
- **Права пісочниці (`finish-args`):** `--share=network`, `--share=ipc`, `--socket=x11`, `--socket=pulseaudio`, `--device=dri`, `--filesystem=~/.minecraft:create`, `--filesystem=xdg-run/discord-ipc-0`.

## Вміст директорії

- `io.github.wer_core.cl-opensource.yml` — Flatpak-маніфест для збірки з вихідного коду (`--self-contained false` з використанням `org.freedesktop.Sdk.Extension.dotnet10` та `nuget-sources.json`).
- `io.github.wer_core.cl-opensource.desktop` — `.desktop`-файл для інтеграції з меню застосунків Linux.
- `io.github.wer_core.cl-opensource.metainfo.xml` — AppStream-метадані для центрів застосунків (KDE Discover, GNOME Software).
- `io.github.wer_core.cl-opensource.png` — іконка застосунку `512x512`.

## Локальна збірка та тестування

### 1. Встановлення залежностей

```bash
# Ubuntu / Debian
sudo apt install flatpak flatpak-builder

# Arch Linux / Manjaro
sudo pacman -S flatpak flatpak-builder

# Fedora
sudo dnf install flatpak flatpak-builder
```

Встановіть runtime, SDK та розширення .NET 10:

```bash
flatpak install org.freedesktop.Platform//26.08 org.freedesktop.Sdk//26.08 org.freedesktop.Sdk.Extension.dotnet10//26.08
```

### 2. Збірка та перевірка локально

```bash
flatpak-builder --user --install --force-clean build-dir distribution/flatpak/io.github.wer_core.cl-opensource.yml
flatpak run io.github.wer_core.cl-opensource
```

## Власний OSTree-репозиторій (GitHub Pages)

Офіційне розповсюдження Flatpak відбувається через власний OSTree-репозиторій на гілці `gh-pages` репозиторію `WER-CORE/CL-OpenSource`:

- **OSTree URL:** `https://wer-core.github.io/CL-OpenSource/repo` (гілка `stable`, `archive-z2`, `indexed-deltas=true`)
- **Файли підключення:** `cl.flatpakrepo` та `cl.flatpakref` (розміщуються у корені `gh-pages`)
- **Публікація:** під час стабільного релізу CI-пайплайн `.github/workflows/release.yml` пакує вже зібраний self-contained бінарник (`./publish/linux-x64`), експортує його в OSTree-репозиторій, підписує GPG-ключем (`flatpak build-update-repo --gpg-sign ... --generate-static-deltas --prune`) та оновлює `gh-pages`, а також додає до GitHub Release автономний бандл `CLegendaryLauncher-x64.flatpak`.
