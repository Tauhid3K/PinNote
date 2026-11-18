#!/usr/bin/env bash
set -e

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VERSION="1.1.0"
PROJECT="$REPO_ROOT/PinNote.csproj"
PUBLISH_DIR="$REPO_ROOT/artifacts/publish/linux-x64"
DIST_DIR="$REPO_ROOT/artifacts/package"

echo "=== Building PinNote for Linux (x64) ==="

mkdir -p "$PUBLISH_DIR"
mkdir -p "$DIST_DIR"

# 1. Publish self-contained executable for Linux
dotnet publish "$PROJECT" \
    -f net8.0 \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:EnableCompressionInSingleFile=true \
    -p:DebugType=None \
    -p:DebugSymbols=false \
    -p:Version=$VERSION \
    -o "$PUBLISH_DIR"

# Ensure SkiaSharp and HarfBuzz native libraries are present alongside binary
cp "$REPO_ROOT/bin/Release/net8.0/linux-x64"/lib*Sharp.so "$PUBLISH_DIR/" 2>/dev/null || true

# 2. Prepare staging directory for .tar.gz packaging
STAGING_DIR="$REPO_ROOT/artifacts/staging/PinNote"
rm -rf "$STAGING_DIR"
mkdir -p "$STAGING_DIR"

cp -r "$PUBLISH_DIR"/* "$STAGING_DIR/"
if [ -d "$REPO_ROOT/Assets" ]; then
    cp -r "$REPO_ROOT/Assets" "$STAGING_DIR/"
fi
chmod +x "$STAGING_DIR/PinNote"

# Create standard Linux .desktop application launcher
cat << 'EOF' > "$STAGING_DIR/PinNote.desktop"
[Desktop Entry]
Type=Application
Version=1.0
Name=PinNote
Comment=PinNote Sticky Notes
Exec=PinNote
Icon=pinnote
Terminal=false
Categories=Utility;Office;
StartupNotify=false
EOF

# 3. Create .tar.gz archive
ARCHIVE_NAME="PinNote-$VERSION-linux-x64.tar.gz"
tar -czf "$DIST_DIR/$ARCHIVE_NAME" -C "$REPO_ROOT/artifacts/staging" PinNote

echo "=== Linux package ready: $DIST_DIR/$ARCHIVE_NAME ==="

# 4. Create self-extracting Linux installer .sh in artifacts/installer
INSTALLER_DIR="$REPO_ROOT/artifacts/installer"
mkdir -p "$INSTALLER_DIR"
INSTALLER_SH="$INSTALLER_DIR/PinNote-Setup-$VERSION.sh"

cat << 'HEADER_EOF' > "$INSTALLER_DIR/.header.tmp"
#!/usr/bin/env bash
# PinNote Linux Self-Extracting Installer
set -e

INSTALL_DIR="$HOME/.local/share/pinnote"
BIN_DIR="$HOME/.local/bin"
DESKTOP_DIR="$HOME/.local/share/applications"
ICON_DIR="$HOME/.local/share/icons/hicolor/128x128/apps"

echo "============================================="
echo "   PinNote Linux Setup"
echo "============================================="
echo "Installing PinNote to $INSTALL_DIR..."

pkill -x PinNote 2>/dev/null || true

# Clean existing installation directory to prevent stale/obsolete files from lingering across upgrades
if [ -d "$INSTALL_DIR" ]; then
    rm -rf "$INSTALL_DIR"
fi

mkdir -p "$INSTALL_DIR"
mkdir -p "$BIN_DIR"
mkdir -p "$DESKTOP_DIR"
mkdir -p "$ICON_DIR"
mkdir -p "$HOME/.local/share/pixmaps"

TMP_DIR=$(mktemp -d)
trap 'rm -rf "$TMP_DIR"' EXIT

ARCHIVE_LINE=$(grep -a -n '^__PAYLOAD_BEGINS_HERE__$' "$0" | head -n 1 | cut -d: -f1)
tail -n +"$((ARCHIVE_LINE + 1))" "$0" | tar -xz -C "$TMP_DIR"

if [ -d "$TMP_DIR/PinNote" ]; then
    cp -r "$TMP_DIR/PinNote"/* "$INSTALL_DIR/"
else
    cp -r "$TMP_DIR"/* "$INSTALL_DIR/"
fi

chmod +x "$INSTALL_DIR/PinNote"
ln -sf "$INSTALL_DIR/PinNote" "$BIN_DIR/pinnote"

if [ -f "$INSTALL_DIR/Assets/icon.png" ]; then
    cp "$INSTALL_DIR/Assets/icon.png" "$HOME/.local/share/pixmaps/pinnote.png"
    for size in 16x16 32x32 48x48 64x64 128x128 256x256; do
        mkdir -p "$HOME/.local/share/icons/hicolor/$size/apps"
        cp "$INSTALL_DIR/Assets/icon.png" "$HOME/.local/share/icons/hicolor/$size/apps/pinnote.png"
    done
fi

cat << 'DESKTOPEOF' > "$DESKTOP_DIR/pinnote.desktop"
[Desktop Entry]
Type=Application
Version=1.0
Name=PinNote
Comment=Lightweight Sticky Note Application
Exec=pinnote
Icon=pinnote
Terminal=false
Categories=Utility;Office;
StartupNotify=false
DESKTOPEOF

chmod +x "$DESKTOP_DIR/pinnote.desktop"
update-desktop-database "$DESKTOP_DIR" 2>/dev/null || true
gtk-update-icon-cache -f -t "$HOME/.local/share/icons/hicolor" 2>/dev/null || true

echo ""
echo "=== PinNote installed successfully! ==="
echo "  - Installed executable: $INSTALL_DIR/PinNote"
echo "  - Terminal command: pinnote (via $BIN_DIR/pinnote)"
echo "  - Desktop shortcut: $DESKTOP_DIR/pinnote.desktop"
echo "You can launch PinNote now from your app launcher or by running 'pinnote'."
exit 0

__PAYLOAD_BEGINS_HERE__
HEADER_EOF

cat "$INSTALLER_DIR/.header.tmp" "$DIST_DIR/$ARCHIVE_NAME" > "$INSTALLER_SH"
rm -f "$INSTALLER_DIR/.header.tmp"
chmod +x "$INSTALLER_SH"

echo "=== Linux installer ready: $INSTALLER_SH ==="
