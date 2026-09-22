#!/usr/bin/env bash
set -euo pipefail

dotnet publish InvoiceGenerator.csproj \
  --configuration Release \
  --runtime win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:EnableCompressionInSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  --output publish/win-x64

echo
echo "Windows app published to:"
echo "publish/win-x64/InvoiceGenerator.exe"
