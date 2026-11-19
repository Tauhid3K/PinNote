#!/usr/bin/env bash
set -e

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

if [ -z "$1" ]; then
    echo "Usage: ./scripts/release.sh <version> (e.g. ./scripts/release.sh 1.0.1)"
    exit 1
fi

VERSION="${1#v}" # strip leading 'v' if present
TAG="v$VERSION"

echo "========================================="
echo " Preparing PinNote Release $TAG"
echo "========================================="

# 1. Update PinNote.csproj version fields
echo "Updating PinNote.csproj to version $VERSION..."
sed -i "s|<Version>.*</Version>|<Version>$VERSION</Version>|g" "$REPO_ROOT/PinNote.csproj"
sed -i "s|<AssemblyVersion>.*</AssemblyVersion>|<AssemblyVersion>$VERSION.0</AssemblyVersion>|g" "$REPO_ROOT/PinNote.csproj"
sed -i "s|<FileVersion>.*</FileVersion>|<FileVersion>$VERSION.0</FileVersion>|g" "$REPO_ROOT/PinNote.csproj"
sed -i "s|<InformationalVersion>.*</InformationalVersion>|<InformationalVersion>$VERSION</InformationalVersion>|g" "$REPO_ROOT/PinNote.csproj"

# 2. Update build-linux.sh version
sed -i "s|VERSION=\".*\"|VERSION=\"$VERSION\"|g" "$REPO_ROOT/scripts/build-linux.sh"

# 3. Build Linux installer and archive locally
echo "Building Linux packages..."
"$REPO_ROOT/scripts/build-linux.sh"

echo "========================================="
echo " Local packages built for $TAG!"
echo "========================================="
echo ""
echo "To publish this release automatically via GitHub Actions:"
echo "  git add ."
echo "  git commit -m \"chore: Release $TAG\""
echo "  git tag $TAG"
echo "  git push origin master"
echo "  git push origin $TAG"
echo ""
echo "GitHub Actions will automatically:"
echo "  1. Compile Windows (.exe & .zip) and Linux (.sh & .tar.gz)"
echo "  2. Create the GitHub Release $TAG"
echo "  3. Upload all 4 installer packages"
echo "  4. Your website will instantly auto-sync and serve the new version!"
echo "========================================="
