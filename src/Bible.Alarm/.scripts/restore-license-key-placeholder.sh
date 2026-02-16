#!/usr/bin/env bash
# Restore license key placeholder in AppSettings.cs after build

set -e
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
SOURCE_FILE="$PROJECT_ROOT/AppSettings.cs"
PLACEHOLDER='{{SYNCFUSION_LICENSE_KEY}}'

if [ ! -f "$SOURCE_FILE" ]; then
  echo "AppSettings.cs not found at $SOURCE_FILE. Skipping restore."
  exit 0
fi

if grep -qF "$PLACEHOLDER" "$SOURCE_FILE"; then
  echo "AppSettings.cs already contains placeholder. No restore needed."
  exit 0
fi

# Restore placeholder (match SyncfusionLicenseKey => "anything")
if grep -q 'SyncfusionLicenseKey[[:space:]]*=>[[:space:]]*"[^"]*"' "$SOURCE_FILE"; then
  # Use perl for portable in-place replace with special-character-safe pattern
  perl -i -pe 's/SyncfusionLicenseKey\s*=>\s*"[^"]*"/SyncfusionLicenseKey => "{{SYNCFUSION_LICENSE_KEY}}"/' "$SOURCE_FILE"
  echo "License key placeholder restored in AppSettings.cs"
else
  echo "Could not find license key pattern in AppSettings.cs. Skipping restore."
fi

exit 0
