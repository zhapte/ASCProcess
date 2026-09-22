#!/usr/bin/env bash
set -euo pipefail

dotnet publish InvoiceGenerator.csproj \
  --configuration Release \
  --runtime win-x64 \
  --self-contained true \
  --output publish/win-x64-folder

echo
echo "Windows self-contained folder published to:"
echo "publish/win-x64-folder"
echo
echo "Copy the whole folder to the Windows laptop, then run:"
echo "RunInvoiceGenerator.bat"
