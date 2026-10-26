#!/usr/bin/env bash
set -e

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# Ensure linux build is published
if [ ! -f "$REPO_ROOT/artifacts/publish/linux-x64/PinNote" ]; then
    echo "Linux binaries not found. Building now..."
    "$REPO_ROOT/scripts/build-linux.sh"
fi

if ! command -v flatpak-builder &>/dev/null; then
    echo "Error: flatpak-builder is not installed."
    echo "On Arch/CachyOS, install it via: sudo pacman -S flatpak-builder"
    echo "On Ubuntu/Debian, install via: sudo apt install flatpak-builder"
    exit 1
fi

echo "=== Building and installing PinNote Flatpak ==="

cd "$REPO_ROOT/installer/linux"
flatpak-builder --user --install --force-clean build-dir org.pinnote.PinNote.yaml

echo ""
echo "=== Flatpak installed successfully! ==="
echo "Run via: flatpak run org.pinnote.PinNote"
