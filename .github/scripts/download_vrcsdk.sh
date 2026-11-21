#!/usr/bin/env bash
set -euo pipefail

VERSION="${1:?usage: download_vrcsdk.sh <version> <kind>}"
SDK_KIND="${2:?usage: download_vrcsdk.sh <version> <kind>}"
UNITY_VERSION="${3:-}"
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
  local samples="$PROJECT_PATH/Packages/$package_name/Samples"
  if [[ "$package_name" == "com.vrchat.avatars" ]]; then
    # VRCFury loads the default controllers by GUID. Keep their animation dependencies
    # and metadata, but avoid importing the robot avatar and other unused samples.
    test -d "$samples/AV3 Demo Assets/Animation"
    find "$samples" -mindepth 1 -maxdepth 1 \
      ! -name 'AV3 Demo Assets' ! -name 'AV3 Demo Assets.meta' -exec rm -rf {} +
    find "$samples/AV3 Demo Assets" -mindepth 1 -maxdepth 1 \
      ! -name 'Animation' ! -name 'Animation.meta' -exec rm -rf {} +
  else
    rm -rf "$samples" "$samples.meta"
  fi
}

patch_udonsharp_for_ci() {
  # vrcsdk world sdk crashes during startup in fresh batch mode
  # unless we prevent the udonsharp locator from initializing
  local locator_file="$PROJECT_PATH/Packages/com.vrchat.worlds/Integrations/UdonSharp/Runtime/UdonSharpDataLocator.cs"
  perl -0pi -e 's/foundLocators\.Add\(InitializeUdonSharpData\(\)\);/return "Assets\/UdonSharp";/' "$locator_file"

  # The fresh import already loads the SDK DLLs. Skip their forced second import and domain reload in batch mode.
  mkdir -p "$PROJECT_PATH/Library"
  printf '%s' WORLDS_REIMPORTED > "$PROJECT_PATH/Library/WORLDS_REIMPORTED"

  # Seed SDK/world defines before the first compile instead of forcing a reload during tests.
  local project_settings="$PROJECT_PATH/ProjectSettings/ProjectSettings.asset"
  local define
  local world_defines=(UDON UDONSHARP)
  if [[ -n "$(find "$PROJECT_PATH/Packages/com.vrchat.worlds" -name ClientSimPlayerDataStorage.cs -print -quit)" ]]; then
    world_defines+=(VRC_ENABLE_PLAYER_PERSISTENCE)
  fi
  for define in "${world_defines[@]}"; do
    SDK_DEFINE="$define" perl -0pi -e 's/^(    (?:Android|Standalone): (?![^\r\n]*\b\Q$ENV{SDK_DEFINE}\E\b)[^\r\n]*\bVRCF_TEST\b[^\r\n]*)(\r?)$/$1;$ENV{SDK_DEFINE}$2/gm' "$project_settings"
  done
  if ! grep -Eq 'Standalone: .*VRCF_TEST.*;UDONSHARP' "$project_settings"; then
    echo "Failed to add the UdonSharp project define" >&2
    exit 1
  fi
}

patch_unity_6_5_for_ci() {
  local base="$PROJECT_PATH/Packages/com.vrchat.base"
  local avatars="$PROJECT_PATH/Packages/com.vrchat.avatars/Editor/VRCSDK/SDK3A"

  # UniTask's tracker uses integer task IDs with the generic TreeView APIs.
  perl -pi -e 's/\b(TreeView|TreeViewItem|TreeViewState)\b(?!<)/$1<int>/g' \
    "$base/Editor/VRCSDK/Plugins/UniTask/Editor/UniTaskTrackerTreeView.cs"
  perl -pi -e 's/HashSet<int>/HashSet<EntityId>/g if /\bscannedObjects\b/; s/obj\.GetInstanceID\(\)/obj.GetEntityId()/g' \
    "$base/Runtime/VRCSDK/Dependencies/VRChat/Scripts/Validation/WorldValidation.cs"
  perl -pi -e 's/AssetDatabase\.GetAssetPath\(clip\.GetInstanceID\(\)\)/AssetDatabase.GetAssetPath(clip)/g' \
    "$avatars/VRCSdkControlPanelAvatarBuilder.cs"
  perl -pi -e 's/\bint(?= target, string path|, string\),)/EntityId/g; s/\.GetInstanceID\(\)/.GetEntityId()/g' \
    "$avatars/Components3/FaceTrackingEditorContext.cs" \
    "$avatars/Components3/FaceTrackingBlendshapesSetDrawer.cs" \
    "$avatars/Components3/FaceTrackingParameterBehaviorDrawer.cs"
  # XR Plug-in Management configures VR in Unity 6.6.
  perl -pi -e 's/^\s*"com\.unity\.modules\.vr": "[^"]+",\r?\n$//' "$PROJECT_PATH/Packages/manifest.json"
  perl -0pi -e 's/^ +if \(!EditorApplication\.isPlaying\)\r?\n +\{\r?\n +#pragma warning disable 618\r?\n +PlayerSettings\.SetVirtualRealitySupported\([^\r\n]+\r?\n +#pragma warning restore 618\r?\n +\}\r?\n//m' \
    "$base/Editor/VRCSDK/Dependencies/VRChat/EnvConfig.cs"
  dotnet run --project "$(dirname "${BASH_SOURCE[0]}")/UnitySdkMathPatch" -- \
    "$base/Runtime/VRCSDK/Plugins/VRC.Dynamics.dll"
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

if [[ "$VERSION" == 3.11.0-beta.1 && "$SDK_KIND" == avatars && "$UNITY_VERSION" =~ ^([0-9]+)\.([0-9]+)\. ]] &&
  (( BASH_REMATCH[1] > 6000 || (BASH_REMATCH[1] == 6000 && BASH_REMATCH[2] >= 5) )); then
  patch_unity_6_5_for_ci
fi

if [[ "$SDK_KIND" == "worlds" ]]; then
  patch_udonsharp_for_ci
fi
