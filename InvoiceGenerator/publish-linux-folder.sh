#!/usr/bin/env bash
set -euo pipefail

dotnet publish InvoiceGenerator.csproj \
  --configuration Release \
  --runtime linux-x64 \
  --self-contained true \
  --output publish/linux-x64-folder

cp RunInvoiceGenerator.sh publish/linux-x64-folder/
chmod +x publish/linux-x64-folder/InvoiceGenerator
chmod +x publish/linux-x64-folder/RunInvoiceGenerator.sh

echo
echo "Linux self-contained folder published to:"
echo "publish/linux-x64-folder"
echo
echo "Copy the whole folder to the Linux machine, then run:"
echo "./RunInvoiceGenerator.sh"
