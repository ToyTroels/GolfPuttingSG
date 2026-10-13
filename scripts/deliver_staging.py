"""Validate, migrate, and deploy the configured staging service after CI passes."""

import argparse
from dataclasses import dataclass
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import time
from urllib.error import HTTPError, URLError
from urllib.parse import parse_qs, urlencode, unquote, urlsplit, urlunsplit
from urllib.request import Request, urlopen

ROOT = Path(__file__).resolve().parents[1]


@dataclass(frozen=True)
class Delivery:
    metadata: dict
    commit: str
    hook: str
    database_connection: str
    password: str

    @property
    def api_address(self):
        return self.metadata["apiAddress"].rstrip("/")


def configuration(environment=None, metadata=None):
    environment = os.environ if environment is None else environment
    if metadata is None:
        metadata = json.loads((ROOT / "deploy/staging.json").read_text(encoding="utf-8"))
    if environment.get("GITHUB_ACTIONS") == "true" and (
        environment.get("GITHUB_REF") != "refs/heads/dev"
        or environment.get("GITHUB_EVENT_NAME") != "push"
    ):
        raise ValueError("Staging delivery requires a push to dev.")
    commit = environment.get("GITHUB_SHA", "")
    if not re.fullmatch(r"[0-9a-fA-F]{40}", commit):
        raise ValueError("A full validated commit SHA is required.")
    hook_value = environment.get("STAGING_RENDER_DEPLOY_HOOK", "").strip()
    if not hook_value:
        raise ValueError("Add the STAGING_RENDER_DEPLOY_HOOK Actions secret.")
    hook = urlsplit(hook_value)
    if (
        hook.scheme != "https"
        or hook.hostname != "api.render.com"
        or hook.port not in (None, 443)
        or hook.username or hook.password or hook.fragment
        or hook.path.rstrip("/") != "/deploy/" + metadata["render"]["serviceId"]
    ):
        raise ValueError("The deploy hook must belong to the configured staging service.")
    query = parse_qs(hook.query)
    if len(query.get("key", [])) != 1 or not query["key"][0]:
        raise ValueError("The staging deploy hook is missing its key.")
    query["ref"] = [commit.lower()]
    deployment_hook = urlunsplit(hook._replace(query=urlencode(query, doseq=True)))

    database_value = environment.get("STAGING_DATABASE_SETUP_CONNECTION", "").strip()
    if not database_value:
        raise ValueError("Add the STAGING_DATABASE_SETUP_CONNECTION Actions secret.")
    database = urlsplit(database_value)
    expected = metadata["neon"]
    if (
        database.scheme not in ("postgres", "postgresql")
        or database.hostname != expected["directHost"]
        or database.port not in (None, 5432)
        or unquote(database.path.lstrip("/")) != expected["database"]
        or unquote(database.username or "") != expected["setupRole"]
        or not database.password or database.fragment
    ):
        raise ValueError("Use the direct Neon URL for the staging owner, host, and database recorded in deploy/staging.json.")
    ssl_mode = parse_qs(database.query).get("sslmode", ["require"])[0]
    if ssl_mode not in ("require", "verify-ca", "verify-full"):
        raise ValueError("The staging database connection must require TLS.")
    password = unquote(database.password)
    if any(c in password for c in "\r\n\0"):
        raise ValueError("The database password contains unsupported control characters.")

    def quoted(value):
        return '"' + value.replace('"', '""') + '"'

    npgsql_ssl_mode = {"require": "Require", "verify-ca": "VerifyCA", "verify-full": "VerifyFull"}[ssl_mode]
    connection = (
        f"Host={expected['directHost']};Port=5432;Database={quoted(expected['database'])};"
        f"Username={quoted(expected['setupRole'])};Password={quoted(password)};"
        f"SSL Mode={npgsql_ssl_mode};Channel Binding=Require"
    )
    return Delivery(metadata, commit.lower(), deployment_hook, connection, password)


def migrate(delivery):
    environment = dict(os.environ)
    environment["ConnectionStrings__DatabaseSetup"] = delivery.database_connection
    environment["DatabaseSetup__ApplicationRole"] = delivery.metadata["neon"]["runtimeRole"]
    project = "src/GolfSG.Database.Setup/GolfSG.Database.Setup.csproj"
    subprocess.run(["dotnet", "restore", project, "--locked-mode"], cwd=ROOT, check=True, timeout=180)
    subprocess.run(
        ["dotnet", "run", "--project", project, "-c", "Release", "--no-restore"],
        cwd=ROOT, env=environment, check=True, timeout=300,
    )


def read_json(address):
    request = Request(address, headers={"Accept": "application/json", "Cache-Control": "no-cache"})
    with urlopen(request, timeout=15) as response:
        return json.load(response)


def serving_commit(delivery):
    try:
        deployed = read_json(delivery.api_address + "/api/deployment")
        health = read_json(delivery.api_address + "/api/health")
        return deployed.get("commit") == delivery.commit and health.get("databaseAvailable") is True
    except (HTTPError, URLError, TimeoutError, ValueError):
        return False


def deploy(delivery, timeout_seconds=900):
    if serving_commit(delivery):
        print("Staging already serves the validated commit and its database is ready.", flush=True)
        return
    try:
        with urlopen(Request(delivery.hook, data=b"", method="POST"), timeout=30) as response:
            response.read()
    except HTTPError as error:
        raise RuntimeError(f"Render rejected the staging deployment request (HTTP {error.code}).") from None
    except (URLError, TimeoutError):
        raise RuntimeError("The staging deployment request failed; check the Render service events.") from None

    deadline = time.monotonic() + timeout_seconds
    while time.monotonic() < deadline:
        if serving_commit(delivery):
            print(f"Staging deployment verified: {delivery.commit}", flush=True)
            return
        print("Waiting for staging to serve the validated commit with a ready database...", flush=True)
        time.sleep(10)
    raise RuntimeError("Staging did not serve the validated commit before the deployment timeout. Check Render deploy logs.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("operation", choices=["validate", "migrate", "deploy"])
    operation = parser.parse_args().operation
    try:
        delivery = configuration()
        if os.environ.get("GITHUB_ACTIONS") == "true":
            # The normalized connection is different from the original secret URL.
            # Mask its password as well so a driver error cannot reveal it.
            print("::add-mask::" + delivery.password.replace("%", "%25"), flush=True)
        if operation == "migrate":
            migrate(delivery)
        elif operation == "deploy":
            deploy(delivery)
        else:
            print("Staging deployment targets and credentials validated.")
        return 0
    except (ValueError, RuntimeError) as error:
        print(str(error), file=sys.stderr)
    except (subprocess.CalledProcessError, subprocess.TimeoutExpired):
        print("Database setup failed; staging deployment stopped.", file=sys.stderr)
    return 1


if __name__ == "__main__":
    sys.exit(main())
