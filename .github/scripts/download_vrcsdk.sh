#!/usr/bin/env bash
set -euo pipefail

VERSION="${1:?usage: download_vrcsdk.sh <version> <kind>}"
SDK_KIND="${2:?usage: download_vrcsdk.sh <version> <kind>}"
PROJECT_PATH="${PROJECT_PATH:?PROJECT_PATH must be set}"

echo "Downloading VRCSDK $VERSION"

download_manifest_package() {
  local package_name="$1"
  local version="$2"
  local zip_path="$package_name.zip"
  local url

  url="$(echo "$MANIFEST" | jq ".packages.\"$package_name\".versions.\"$version\".url" --raw-output)"

  echo "$package_name URL is $url"
  curl -L -o "$zip_path" "$url"
  mkdir -p "$PROJECT_PATH/Packages/$package_name"
  unzip -o "$zip_path" -d "$PROJECT_PATH/Packages/$package_name"
  rm "$zip_path"
  rm -rf "$PROJECT_PATH/Packages/$package_name/Samples" "$PROJECT_PATH/Packages/$package_name/Samples.meta"
}

patch_udonsharp_locator() {
  # vrcsdk world sdk crashes during startup in fresh batch mode
  # unless we prevent the udonsharp locator from initializing
  local locator_file="$PROJECT_PATH/Packages/com.vrchat.worlds/Integrations/UdonSharp/Runtime/UdonSharpDataLocator.cs"
  perl -0pi -e 's/foundLocators\.Add\(InitializeUdonSharpData\(\)\);/return "Assets\/UdonSharp";/' "$locator_file"
}

MANIFEST_URL="https://vrchat.github.io/packages/index.json"
echo "Downloading manifest from $MANIFEST_URL"
MANIFEST="$(curl "$MANIFEST_URL")"
download_manifest_package "com.vrchat.base" "$VERSION"

if [[ "$SDK_KIND" == "worlds" ]]; then
  SDK_PACKAGE="com.vrchat.worlds"
else
  SDK_PACKAGE="com.vrchat.avatars"
fi

download_manifest_package "$SDK_PACKAGE" "$VERSION"

if [[ "$SDK_KIND" == "worlds" ]]; then
  patch_udonsharp_locator
fi
