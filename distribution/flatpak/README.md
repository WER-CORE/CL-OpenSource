# CLegendary Launcher — Flatpak & Flathub Integration

Ця директорія містить файли конфігурації для пакування та публікації **CLegendary Launcher** у форматі Flatpak та каталозі [Flathub](https://flathub.org).

## Вміст директорії

- `app.clegendary.launcher.yml` — маніфест Flatpak з налаштуваннями ізоляції (пісочниці), залежностями Wayland/X11, аудіо та файлової системи.
- `app.clegendary.launcher.desktop` — файл інтеграції з робочим столом Linux (меню додатків, категорії).
- `app.clegendary.launcher.metainfo.xml` — метадані AppStream для графічних магазинів додатків (GNOME Software, KDE Discover, Flathub).
- `app.clegendary.launcher.png` — векторна/растрова іконка лаунчера у роздільній здатності 512x512.

---

## 🛠 Локальна збірка та тестування (Linux / SteamOS)

### 1. Встановлення інструментів
Для складання потрібен `flatpak` та `flatpak-builder`:
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

### 2. Складання та встановлення
```bash
# Збірка додатку у локальну директорію
flatpak-builder --user --install --force-clean build-dir app.clegendary.launcher.yml

# Запуск лаунчера у пісочниці Flatpak
flatpak run app.clegendary.launcher
```

---

## 🚀 Публікація у Flathub

1. Створіть форк офіційного репозиторію [flathub/flathub](https://github.com/flathub/flathub).
2. Створіть нову гілку `new-pr/app.clegendary.launcher`.
3. Додайте маніфест `app.clegendary.launcher.yml`, `.desktop`, `.metainfo.xml` та іконку.
4. Відкрийте Pull Request до `flathub/flathub`.
5. Автоматичний бот Flathub проведе валідацію (AppStream linter, перевірка ліцензії та пісочниці) та згенерує тестову збірку для перевірки.
6. Після затвердження модераторами репозиторій додатку буде створено на `https://github.com/flathub/app.clegendary.launcher`, а лаунчер з'явиться в Flathub і Steam Deck Discover!
