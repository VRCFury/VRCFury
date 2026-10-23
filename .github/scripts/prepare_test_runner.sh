#!/usr/bin/env bash
set -euo pipefail

# Leave room for Docker's compressed layers, the unpacked editor, and Unity's imports.
# Cached Libraries are restored before this check, so their disk usage is already included.
required_bytes=$((20 * 1024 * 1024 * 1024))
available_bytes() {
  df -B1 --output=avail "${GITHUB_WORKSPACE:?GITHUB_WORKSPACE must be set}" | tail -n 1
}

echo "Available disk space before Unity: $(available_bytes) bytes"
# Stop as soon as we have enough space. Avoid deleting every preinstalled toolchain
# (several minutes on hosted runners), and do not prune an already-present Unity image.
for toolchain in /opt/hostedtoolcache/CodeQL /usr/local/lib/android /usr/share/dotnet /opt/ghc; do
  if (( $(available_bytes) >= required_bytes )); then break; fi
  if [[ -d "$toolchain" ]]; then
    cleanup_started=$EPOCHSECONDS
    echo "Removing unused host toolchain: $toolchain"
    sudo rm -rf -- "$toolchain"
    echo "Cleanup took $((EPOCHSECONDS - cleanup_started))s; available: $(available_bytes) bytes"
  fi
done
if (( $(available_bytes) < required_bytes )); then
  echo "::error::Not enough free disk space for Unity (need 20 GiB)."
  exit 1
fi

docker pull "${UNITY_TEST_IMAGE:?UNITY_TEST_IMAGE must be set}"
