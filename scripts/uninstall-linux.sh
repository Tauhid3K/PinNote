#!/usr/bin/env bash
set -e

INSTALL_DIR="$HOME/.local/share/pinnote"
BIN_DIR="$HOME/.local/bin"
DESKTOP_DIR="$HOME/.local/share/applications"
ICON_DIR="$HOME/.local/share/icons/hicolor/128x128/apps"

echo "=== Uninstalling PinNote ==="

# Kill running instances if any
pkill -x PinNote 2>/dev/null || true

rm -rf "$INSTALL_DIR"
rm -f "$BIN_DIR/pinnote"
rm -f "$DESKTOP_DIR/pinnote.desktop"
rm -f "$ICON_DIR/pinnote.png"

update-desktop-database "$DESKTOP_DIR" 2>/dev/null || true
gtk-update-icon-cache -f -t "$HOME/.local/share/icons/hicolor" 2>/dev/null || true

echo "PinNote has been removed from your system."
