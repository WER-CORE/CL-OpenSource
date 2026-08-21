#!/usr/bin/env bash
# Реєструє CL Launcher у стільниці: ярлик у меню застосунків та іконка
# на панелі завдань. Без цього Wayland/X11 не має чим позначити вікно.
#
#   ./install-desktop-entry.sh [шлях-до-каталогу-збірки]
#
# Типово береться збірка Debug поруч із цим скриптом.

set -euo pipefail

APP_ID="cl-launcher"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_DIR="${1:-$SCRIPT_DIR/../bin/Debug/net8.0}"

DLL="$BUILD_DIR/CL.Avalonia.dll"
ICON_SRC="$SCRIPT_DIR/../Assets/icon.png"

if [[ ! -f "$DLL" ]]; then
    echo "Не знайдено $DLL" >&2
    echo "Спочатку виконай: dotnet build CL.Crossplatform.sln" >&2
    exit 1
fi

DESKTOP_DIR="$HOME/.local/share/applications"
ICON_DIR="$HOME/.local/share/icons/hicolor/256x256/apps"

mkdir -p "$DESKTOP_DIR" "$ICON_DIR"
install -m 644 "$ICON_SRC" "$ICON_DIR/$APP_ID.png"

# Проєкт націлений на net8.0, а в системі може бути лише новіший runtime.
cat > "$DESKTOP_DIR/$APP_ID.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=CL Launcher
GenericName=Minecraft Launcher
Comment=Запуск Minecraft (Linux Edition)
Exec=env DOTNET_ROLL_FORWARD=Major dotnet "$(realpath "$DLL")"
Path=$(realpath "$BUILD_DIR")
Icon=$APP_ID
Terminal=false
Categories=Game;
StartupNotify=true
StartupWMClass=$APP_ID
EOF

chmod 644 "$DESKTOP_DIR/$APP_ID.desktop"

command -v update-desktop-database >/dev/null 2>&1 \
    && update-desktop-database "$DESKTOP_DIR" 2>/dev/null || true
command -v gtk-update-icon-cache >/dev/null 2>&1 \
    && gtk-update-icon-cache -f -t "$HOME/.local/share/icons/hicolor" 2>/dev/null || true

echo "Готово: $DESKTOP_DIR/$APP_ID.desktop"

# --- Іконка вікна гри -------------------------------------------------------
# GLFW виставляє вікну гри WM_CLASS з її заголовка разом з номером версії:
# "Minecraft 1.20.4", "Minecraft 26.2" тощо. Лаунчер це значення не змінює,
# тому .desktop-файл створюється для кожної встановленої версії окремо -
# інакше стільниця не зіставить вікно з іконкою.
# Саму іконку беремо з уже завантажених ассетів, копія в репозиторії не потрібна.
GAME_DIR="${CL_GAME_DIR:-$HOME/.clminecraft}"

MC_INDEX="$(ls -1t "$GAME_DIR"/assets/indexes/*.json 2>/dev/null | head -1 || true)"
MC_HASH=""

if [[ -n "$MC_INDEX" ]]; then
    MC_HASH="$(python3 -c 'import json,sys; o=json.load(open(sys.argv[1]))["objects"]; e=o.get("icons/icon_256x256.png"); print(e["hash"] if e else "")' "$MC_INDEX" 2>/dev/null || true)"
fi

MC_ICON="$GAME_DIR/assets/objects/${MC_HASH:0:2}/$MC_HASH"

if [[ -n "$MC_HASH" && -f "$MC_ICON" ]]; then
    # Блок Minecraft із бейджем CL - щоб вікно гри було видно як запущене
    # через цей лаунчер. Якщо Pillow немає, лишаємо чисту іконку Mojang.
    if python3 "$SCRIPT_DIR/make-game-icon.py" \
            "$MC_ICON" "$ICON_SRC" "$ICON_DIR/cl-minecraft.png" 2>/dev/null; then
        chmod 644 "$ICON_DIR/cl-minecraft.png"
    else
        echo "Pillow недоступний - бейдж CL пропущено." >&2
        install -m 644 "$MC_ICON" "$ICON_DIR/cl-minecraft.png"
    fi

    mc_entries=0
    for version_dir in "$GAME_DIR"/versions/*/; do
        [[ -d "$version_dir" ]] || continue

        version="$(basename "$version_dir")"
        [[ -f "$version_dir/$version.json" ]] || continue

        # Пробіли в WM_CLASS допустимі й у StartupWMClass.
        cat > "$DESKTOP_DIR/cl-minecraft-$version.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=Minecraft $version
Comment=Гра, запущена через CL Launcher
Exec=env DOTNET_ROLL_FORWARD=Major dotnet "$(realpath "$DLL")"
Icon=cl-minecraft
Terminal=false
Categories=Game;
NoDisplay=true
StartupWMClass=Minecraft $version
EOF

        chmod 644 "$DESKTOP_DIR/cl-minecraft-$version.desktop"
        mc_entries=$((mc_entries + 1))
    done

    command -v update-desktop-database >/dev/null 2>&1 \
        && update-desktop-database "$DESKTOP_DIR" 2>/dev/null || true
    command -v gtk-update-icon-cache >/dev/null 2>&1 \
        && gtk-update-icon-cache -f -t "$HOME/.local/share/icons/hicolor" 2>/dev/null || true

    if [[ "$mc_entries" -gt 0 ]]; then
        echo "Готово: іконку гри зареєстровано для $mc_entries версій."
    else
        echo "Версій гри не знайдено в $GAME_DIR/versions." >&2
    fi
else
    echo "Іконку гри пропущено: ассети ще не завантажені ($GAME_DIR)." >&2
fi
