#!/usr/bin/env bash
set -e

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TAR_FILE="$REPO_ROOT/artifacts/package/PinNote-1.0.3-linux-x64.tar.gz"

INSTALL_DIR="$HOME/.local/share/pinnote"
BIN_DIR="$HOME/.local/bin"
DESKTOP_DIR="$HOME/.local/share/applications"
ICON_DIR="$HOME/.local/share/icons/hicolor/128x128/apps"

# Ensure package exists
if [ ! -f "$TAR_FILE" ]; then
    echo "Package not found at $TAR_FILE. Building it now..."
    "$REPO_ROOT/scripts/build-linux.sh"
fi

echo "=== Installing PinNote to $INSTALL_DIR ==="

# Stop any running instances before updating files
pkill -x PinNote 2>/dev/null || true
sleep 0.5

mkdir -p "$INSTALL_DIR"
mkdir -p "$BIN_DIR"
mkdir -p "$DESKTOP_DIR"
mkdir -p "$ICON_DIR"

# Extract application files into ~/.local/share/pinnote
tar -xzf "$TAR_FILE" -C "$HOME/.local/share"
# Note: tar contains PinNote/ directory, so it unpacks to $HOME/.local/share/PinNote
if [ -d "$HOME/.local/share/PinNote" ] && [ "$HOME/.local/share/PinNote" != "$INSTALL_DIR" ]; then
    rm -rf "$INSTALL_DIR"
    mv "$HOME/.local/share/PinNote" "$INSTALL_DIR"
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
