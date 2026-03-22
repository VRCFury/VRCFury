#!/bin/bash

set -euo pipefail

FILE_PATH=UnityLicenseFile.ulf
echo "$UNITY_LICENSE" | tr -d '\r' >$FILE_PATH
unity-editor \
    -batchmode \
    -nographics \
    -logFile /dev/stdout \
    -quit \
    -manualLicenseFile $FILE_PATH

if [[ "${UNITY_WARMUP_FIRST:-0}" == "1" ]]; then
  # Worlds SDK import can fail on the first pass when UdonSharp initializes assets
  # during a domain reload/import restart. Warming the project once stabilizes the
  # imported state before the real verification pass.
  unity-editor \
      -batchmode \
      -nographics \
      -logfile /dev/stdout \
      -projectPath /opt/project/.github/workflows/test_project \
      -quit
fi

unity-editor \
    -batchmode \
    -nographics \
    -logfile /dev/stdout \
    -projectPath /opt/project/.github/workflows/test_project \
    -runTests \
    -testPlatform editmode
