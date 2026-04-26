#!/usr/bin/env bash
# Replace license key placeholder in AppSettings.cs with actual key from environment variable

set -e
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
SOURCE_FILE="$PROJECT_ROOT/AppSettings.cs"
PLACEHOLDER='{{SYNCFUSION_LICENSE_KEY}}'
LICENSE_KEY="${SYNCFUSION_LICENSE_KEY:-}"

if [[ -z "$LICENSE_KEY" ]]; then
  echo "SYNCFUSION_LICENSE_KEY environment variable is not set. Using placeholder."
  exit 0
fi

if [[ ! -f "$SOURCE_FILE" ]]; then
  echo "Error: AppSettings.cs not found at $SOURCE_FILE" >&2
  exit 1
fi

if ! grep -qF "$PLACEHOLDER" "$SOURCE_FILE"; then
  echo "License key placeholder not found in AppSettings.cs. Skipping replacement."
  exit 0
fi

# Replace placeholder (perl handles special characters in key)
perl -i -pe 's/\Q{{SYNCFUSION_LICENSE_KEY}}\E/$ENV{SYNCFUSION_LICENSE_KEY}/g' "$SOURCE_FILE"

echo "License key replaced in AppSettings.cs"
exit 0
