# CLegendary Launcher — Flatpak & Flathub Integration

Ця директорія містить файли конфігурації для пакування та автоматизованої публікації **CLegendary Launcher** у каталозі [Flathub](https://flathub.org).

## Архітектура Flathub (Шлях 1 — Prebuilt Binaries)

Flathub використовує готові та перевірені Linux-бінарники, які автоматично генеруються нашим GitHub Actions під час кожного релізу:
- `CLegendaryLauncher-linux-x64.tar.gz` (для x86_64 систем та комп'ютерів)
- `CLegendaryLauncher-linux-arm64.tar.gz` (для aarch64 систем, Raspberry Pi, ARM64)

### Автономне оновлення (`x-checker-data`)
У маніфесті налаштовано блок `x-checker-data`. Офіційний сервіс Flathub (`flatpak-external-data-checker` / `flathubbot`) регулярно перевіряє GitHub API репозиторію `WER-CORE/CL-Win-Edition--Update`. Коли виходить новий реліз:
1. Бот Flathub автоматично фіксує новий тег версії.
2. Розраховує контрольну суму SHA256 нового архіву.
3. Створює pull request та оновлює пакет на Flathub без необхідності ручного редагування коду.

---

## Вміст директорії

- `app.clegendary.launcher.yml` — маніфест Flatpak з налаштуваннями ізоляції (пісочниці), залежностями Wayland/X11, звуком, графічним прискоренням DRI та автооновленням `x-checker-data`.
- `app.clegendary.launcher.desktop` — файл інтеграції з робочим столом Linux (меню додатків, категорії).
- `app.clegendary.launcher.metainfo.xml` — метадані AppStream для графічних центрів додатків (GNOME Software, KDE Discover, Flathub).
- `app.clegendary.launcher.png` — офіційна іконка лаунчера у роздільній здатності 512x512.

---

## 🛠 Локальна збірка та тестування (Linux / SteamOS)

### 1. Встановлення інструментів
```bash
# Ubuntu / Debian
sudo apt install flatpak flatpak-builder

# Arch Linux / Manjaro
sudo pacman -S flatpak flatpak-builder

# Fedora
sudo dnf install flatpak flatpak-builder
```

Встановлення базового runtime freedesktop:
```bash
flatpak install flathub org.freedesktop.Platform//24.08 org.freedesktop.Sdk//24.08
```

### 2. Складання та тестування
```bash
flatpak-builder --user --install --force-clean build-dir app.clegendary.launcher.yml
flatpak run app.clegendary.launcher
```

---

## 🚀 Первинна публікація у Flathub

1. Зробіть Fork репозиторію [flathub/flathub](https://github.com/flathub/flathub).
2. Створіть гілку `new-pr/app.clegendary.launcher`.
3. Додайте 4 файли з цієї директорії (`app.clegendary.launcher.yml`, `.desktop`, `.metainfo.xml`, `.png`).
4. Відкрийте Pull Request.
5. Після перевірки ботом та модераторами лаунчер стане доступним на Flathub та в магазині Discover на Steam Deck.
