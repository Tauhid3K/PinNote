#!/usr/bin/env bash
set -e

VERSION="1.0.0"
INSTALL_DIR="$HOME/.local/share/pinnote"
BIN_DIR="$HOME/.local/bin"
DESKTOP_DIR="$HOME/.local/share/applications"
ICON_DIR="$HOME/.local/share/icons/hicolor/128x128/apps"
RELEASE_URL="https://github.com/Tauhid3K/PinNote/releases/download/v${VERSION}/PinNote-${VERSION}-linux-x64.tar.gz"

TMP_DIR=$(mktemp -d)
trap 'rm -rf "$TMP_DIR"' EXIT

# Detect if running from local repo or standalone curl
SCRIPT_DIR=""
if [ -n "${BASH_SOURCE[0]}" ] && [ -f "${BASH_SOURCE[0]}" ]; then
    SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
fi

LOCAL_REPO=""
if [ -n "$SCRIPT_DIR" ] && [ -f "$SCRIPT_DIR/build-linux.sh" ]; then
    LOCAL_REPO="$(cd "$SCRIPT_DIR/.." && pwd)"
fi

LOCAL_TAR=""
if [ -n "$LOCAL_REPO" ] && [ -f "$LOCAL_REPO/artifacts/package/PinNote-${VERSION}-linux-x64.tar.gz" ]; then
    LOCAL_TAR="$LOCAL_REPO/artifacts/package/PinNote-${VERSION}-linux-x64.tar.gz"
fi

if [ -n "$LOCAL_TAR" ]; then
    echo "Found local package at $LOCAL_TAR"
    TAR_FILE="$LOCAL_TAR"
elif [ -n "$LOCAL_REPO" ] && [ -f "$LOCAL_REPO/scripts/build-linux.sh" ]; then
    echo "Building local package..."
    "$LOCAL_REPO/scripts/build-linux.sh"
    TAR_FILE="$LOCAL_REPO/artifacts/package/PinNote-${VERSION}-linux-x64.tar.gz"
else
    echo "Downloading PinNote v${VERSION} from GitHub releases..."
    TAR_FILE="$TMP_DIR/PinNote-${VERSION}-linux-x64.tar.gz"
    curl -fL "$RELEASE_URL" -o "$TAR_FILE"
fi

echo "=== Installing PinNote to $INSTALL_DIR ==="

# Stop any running instances before updating files
pkill -x PinNote 2>/dev/null || true
sleep 0.5

# Extract package into temporary directory first to prevent conflicts with user data directory
tar -xzf "$TAR_FILE" -C "$TMP_DIR"

# Clean existing installation directory to prevent stale files from lingering across upgrades
if [ -d "$INSTALL_DIR" ]; then
    rm -rf "$INSTALL_DIR"
fi

mkdir -p "$INSTALL_DIR"
mkdir -p "$BIN_DIR"
mkdir -p "$DESKTOP_DIR"
mkdir -p "$ICON_DIR"

if [ -d "$TMP_DIR/PinNote" ]; then
    cp -r "$TMP_DIR/PinNote"/* "$INSTALL_DIR/"
else
    cp -r "$TMP_DIR"/* "$INSTALL_DIR/"
fi

# Ensure executable permissions
chmod +x "$INSTALL_DIR/PinNote"

# Create/update symlink in ~/.local/bin
ln -sf "$INSTALL_DIR/PinNote" "$BIN_DIR/pinnote"

# Install icon to all standard hicolor sizes and pixmaps
mkdir -p "$HOME/.local/share/pixmaps"
if [ -f "$INSTALL_DIR/Assets/icon.png" ]; then
    cp "$INSTALL_DIR/Assets/icon.png" "$HOME/.local/share/pixmaps/pinnote.png"
    for size in 16x16 32x32 48x48 64x64 128x128 256x256; do
        mkdir -p "$HOME/.local/share/icons/hicolor/$size/apps"
        cp "$INSTALL_DIR/Assets/icon.png" "$HOME/.local/share/icons/hicolor/$size/apps/pinnote.png"
    done
fi

# Install desktop entry
cat << EOF > "$DESKTOP_DIR/pinnote.desktop"
[Desktop Entry]
Type=Application
Version=1.0
Name=PinNote
Comment=Lightweight Sticky Note Application
Exec=$INSTALL_DIR/PinNote
Path=$INSTALL_DIR
Icon=pinnote
Terminal=false
Categories=Utility;Office;
StartupNotify=false
EOF

chmod +x "$DESKTOP_DIR/pinnote.desktop"

# Refresh desktop and icon database if tools available
update-desktop-database "$DESKTOP_DIR" 2>/dev/null || true
gtk-update-icon-cache -f -t "$HOME/.local/share/icons/hicolor" 2>/dev/null || true

echo ""
echo "=== PinNote successfully installed! ==="
echo "  - Binary: $INSTALL_DIR/PinNote"
echo "  - Command: pinnote (via $BIN_DIR/pinnote)"
echo "  - Desktop entry: $DESKTOP_DIR/pinnote.desktop"
echo "You can now launch PinNote from your application menu or by running 'pinnote' in terminal."
