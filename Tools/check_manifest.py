import os
import sys
import requests

VERSION = os.environ["GITHUB_SHA"]
ROBUST_CDN_URL = "https://cdn.floofstation.com/"
FORK_ID = "floof-ss14"

def main():
    try:
        data = requests.get(f"{ROBUST_CDN_URL}fork/{FORK_ID}/manifest", timeout=15).json()
    except Exception as e:
        print(f"[ERROR] Failed to fetch manifest: {e}")
        set_output("true")
        return

    builds = data.get("builds", {})

    if VERSION in builds:
        print(f"[INFO] {VERSION} already exists in manifest.")
        print("[INFO] No change detected — aborting gracefully.")
        set_output("false")
        return

    print(f"[INFO] {VERSION} not found in manifest.")
    print("[INFO] Proceeding with build.")
    set_output("true")


def set_output(value: str):
    github_output = os.environ.get("GITHUB_OUTPUT")

    if github_output:
        with open(github_output, "a") as f:
            f.write(f"should_run={value}\n")
    else:
        # fallback (older runner style)
        print(f"::set-output name=should_run::{value}")


if __name__ == "__main__":
    main()
