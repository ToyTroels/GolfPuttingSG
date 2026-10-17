"""Decide whether a push changes API build or delivery inputs."""

import json
import os
from pathlib import Path
import subprocess


API_PATHS = (
    "GolfSG.Api/", "src/GolfSG.Core/", "src/GolfSG.Application/",
    "src/GolfSG.Server.Infrastructure/", "src/GolfSG.Database.Setup/",
    "deploy/", "scripts/",
)
API_FILES = {
    "Dockerfile", ".dockerignore", "global.json", "Directory.Build.props",
    "Directory.Build.targets", "Directory.Packages.props", "NuGet.Config",
    "nuget.config", ".github/workflows/ci.yml",
}


def affects_api(path):
    return path in API_FILES or path.startswith(API_PATHS)


def changed_api(before, after):
    # A new branch or an unavailable force-push base must not silently skip delivery.
    if not before or before == "0" * 40:
        return True
    if subprocess.run(["git", "cat-file", "-e", before + "^{commit}"],
                      stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL).returncode:
        return True
    paths = subprocess.check_output([
        "git", "diff", "--name-only", "--no-renames", "-z", before, after,
    ]).decode("utf-8", errors="surrogateescape").split("\0")
    return any(affects_api(path) for path in paths)


if __name__ == "__main__":
    event = json.loads(Path(os.environ["GITHUB_EVENT_PATH"]).read_text(encoding="utf-8"))
    changed = changed_api(event.get("before"), os.environ["GITHUB_SHA"])
    with open(os.environ["GITHUB_OUTPUT"], "a", encoding="utf-8") as output:
        output.write(f"api={str(changed).lower()}\n")
    print(f"API delivery inputs changed: {changed}")
