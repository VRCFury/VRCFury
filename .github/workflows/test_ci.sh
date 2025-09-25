#!/bin/bash

FILE_PATH=UnityLicenseFile.ulf
echo "$UNITY_LICENSE" | tr -d '\r' >$FILE_PATH
unity-editor \
    -batchmode \
    -nographics \
    -logFile /dev/stdout \
    -quit \
    -manualLicenseFile $FILE_PATH

unity-editor \
    -batchmode \
    -nographics \
    -logfile /dev/stdout \
    -projectPath /opt/project/.github/workflows/test_project \
    -runTests \
    -testPlatform editmode
