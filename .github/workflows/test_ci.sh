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

COVERAGE_OPTIONS="pathFilters:+/**/Assets/Scripts/**;assemblyFilters:+assets;generateAdditionalMetrics;generateHtmlReport;generateBadgeReport"

for platform in "${platforms[@]}"; do
    unity-editor \
        -runTests \
        -projectPath /opt/project/.github/workflows/test_project \
        -testResults /opt/project/Logs/${platform}_test_results.xml \
        -logfile /dev/stdout \
        -debugCodeOptimization \
        -enableCodeCoverage \
        -testPlatform $platform \
        -coverageResultsPath /opt/project/CodeCoverage \
        -coverageOptions $COVERAGE_OPTIONS
done
