#!/bin/bash

FILE_PATH=UnityLicenseFile.ulf
echo "$UNITY_LICENSE" | tr -d '\r' >$FILE_PATH
unity-editor \
    -batchmode \
    -nographics \
    -logFile /dev/stdout \
    -quit \
    -manualLicenseFile $FILE_PATH

platforms=(editmode playmode)

for platform in "${platforms[@]}"; do
    unity-editor \
        -runTests \
        -projectPath /opt/project/.github/workflows/test_project \
        -logfile /dev/stdout \
        -testPlatform $platform \
        -quit
done
