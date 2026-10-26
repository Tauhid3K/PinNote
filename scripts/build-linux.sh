#!/usr/bin/env bash
set -e

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VERSION="1.0.3"
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
