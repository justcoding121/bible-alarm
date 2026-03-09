#!/usr/bin/env python3
"""Check if a build (version + build number) already exists in App Store Connect.
Exits 0 if not found (safe to upload), 1 if found (duplicate, do not upload)."""

import json
import sys
import time
from pathlib import Path

import urllib.error
import urllib.parse
import urllib.request

import jwt


def create_token(key_id: str, issuer_id: str, key_path: Path) -> str:
    with open(key_path) as f:
        key_content = f.read()
    now = int(time.time())
    payload = {
        "iss": issuer_id,
        "iat": now,
        "exp": now + 1200,
        "aud": "appstoreconnect-v1",
    }
    return jwt.encode(
        payload,
        key_content,
        algorithm="ES256",
        headers={"kid": key_id},
    )


def api_get(url: str, token: str) -> dict:
    req = urllib.request.Request(url, headers={"Authorization": f"Bearer {token}"})
    with urllib.request.urlopen(req) as resp:
        return json.loads(resp.read().decode())


def main() -> int:
    if len(sys.argv) != 7:
        print("Usage: check-asc-build-exists.py KEY_ID ISSUER_ID KEY_PATH BUNDLE_ID VERSION BUILD", file=sys.stderr)
        return 2
    _, key_id, issuer_id, key_path, bundle_id, version, build = sys.argv
    key_path = Path(key_path)
    if not key_path.exists():
        print(f"Key file not found: {key_path}", file=sys.stderr)
        return 2

    token = create_token(key_id, issuer_id, key_path)
    base = "https://api.appstoreconnect.apple.com/v1"

    try:
        apps_resp = api_get(
            f"{base}/apps?filter[bundleId]={urllib.parse.quote(bundle_id)}&limit=1",
            token,
        )
    except urllib.error.HTTPError as e:
        body = e.read().decode() if e.fp else ""
        print(f"App Store Connect API error (apps): {e.code} {body}", file=sys.stderr)
        return 2

    data = apps_resp.get("data", [])
    if not data:
        print(f"App not found for bundleId {bundle_id}", file=sys.stderr)
        return 2
    app_id = data[0]["id"]

    try:
        builds_resp = api_get(
            f"{base}/builds?filter[app]={app_id}"
            f"&filter[version]={urllib.parse.quote(build)}"
            f"&filter[preReleaseVersion.version]={urllib.parse.quote(version)}"
            "&limit=1",
            token,
        )
    except urllib.error.HTTPError as e:
        body = e.read().decode() if e.fp else ""
        print(f"App Store Connect API error (builds): {e.code} {body}", file=sys.stderr)
        return 2

    builds = builds_resp.get("data", [])
    if builds:
        print(
            f"Build {version} ({build}) already exists in App Store Connect. Skipping upload to avoid duplicate.",
            file=sys.stderr,
        )
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
