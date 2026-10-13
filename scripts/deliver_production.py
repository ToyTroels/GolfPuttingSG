"""Prepare a release, then migrate and deploy only inside GitHub's protected job."""

import argparse
import json
import os
from pathlib import Path
import re
import subprocess
import sys
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen

import deliver_staging as shared


def metadata():
    return json.loads((shared.ROOT / "deploy/production.json").read_text(encoding="utf-8"))


def github_json(path, environment):
    headers = {"Accept": "application/vnd.github+json", "User-Agent": "GolfSG-production-delivery"}
    if environment.get("GITHUB_TOKEN"):
        headers["Authorization"] = "Bearer " + environment["GITHUB_TOKEN"]
    with urlopen(Request("https://api.github.com" + path, headers=headers), timeout=20) as response:
        return json.load(response)


def verify_release(environment=None, settings=None):
    environment = os.environ if environment is None else environment
    settings = metadata() if settings is None else settings
    if (environment.get("GITHUB_ACTIONS") != "true"
        or environment.get("GITHUB_EVENT_NAME") != "push"
        or environment.get("GITHUB_REF") != "refs/heads/master"
        or environment.get("GITHUB_REPOSITORY") != settings["repository"]):
        raise ValueError("Production delivery requires this repository's master push workflow.")
    commit = environment.get("GITHUB_SHA", "")
    if not re.fullmatch(r"[0-9a-f]{40}", commit):
        raise ValueError("A full validated production commit SHA is required.")
    repository = "/repos/" + settings["repository"]
    protection = github_json(repository + "/environments/production", environment)
    reviewers = [reviewer.get("reviewer", {}).get("login")
                 for rule in protection.get("protection_rules", [])
                 if rule.get("type") == "required_reviewers"
                 for reviewer in rule.get("reviewers", [])]
    # A new/unprotected environment must fail before any production secret is used.
    if reviewers != [settings["requiredReviewer"]] or protection.get("can_admins_bypass") is not False:
        raise ValueError("Production must require ToyTroels approval with administrator bypass disabled.")
    branch_policy = protection.get("deployment_branch_policy") or {}
    policies = github_json(repository + "/environments/production/deployment-branch-policies", environment)
    rules = policies.get("branch_policies", [])
    if (branch_policy.get("custom_branch_policies") is not True
        or branch_policy.get("protected_branches") is not False
        or len(rules) != 1 or rules[0].get("name") != "master"
        or rules[0].get("type", "branch") != "branch"):
        raise ValueError("Only the master branch may access the production environment.")
    head = github_json(repository + "/git/ref/heads/master", environment)
    if head.get("object", {}).get("sha") != commit:
        raise ValueError("This release is outdated; review the latest master run instead.")
    staging = settings["stagingApiAddress"].rstrip("/")
    staged_commit = shared.read_json(staging + "/api/deployment").get("commit", "")
    if not isinstance(staged_commit, str) or not re.fullmatch(r"[0-9a-f]{40}", staged_commit):
        raise ValueError("Staging must report a validated commit before production promotion.")
    if shared.read_json(staging + "/api/health").get("databaseAvailable") is not True:
        raise ValueError("Staging must have a ready database before production promotion.")
    staged_tree = github_json(repository + "/git/commits/" + staged_commit, environment).get("tree", {}).get("sha")
    candidate_tree = subprocess.check_output(["git", "rev-parse", "HEAD^{tree}"], cwd=shared.ROOT, text=True).strip()
    checked_out = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=shared.ROOT, text=True).strip()
    if checked_out != commit or staged_tree != candidate_tree:
        raise ValueError("Production must contain exactly the source files verified in healthy staging.")
    return commit, staged_commit


def review():
    settings = metadata()
    commit, staged = verify_release(settings=settings)
    migrations = sorted(path.name for path in (shared.ROOT / "src/GolfSG.Database.Setup/Sql/Migrations").glob("*.sql"))
    message = (
        "## Production release awaiting your approval\n\n"
        f"Commit: `{commit}`\n\nHealthy staging: `{staged}` (identical source files).\n\n"
        "Tests, database integration, API container and Android build have passed.\n\n"
        "After **ToyTroels** approves, the job checks the protection rules again, applies missing "
        "database migrations transactionally, deploys this exact commit, and verifies API/database readiness.\n\n"
        "Migration files checked: " + ", ".join(f"`{name}`" for name in migrations) + ".\n\n"
        "The first release adds migration 003 for the already-deployed course/library/game API. "
        "It creates five new tables and does not delete existing accounts or rounds.\n"
    )
    print(message)
    if os.environ.get("GITHUB_STEP_SUMMARY"):
        with open(os.environ["GITHUB_STEP_SUMMARY"], "a", encoding="utf-8") as summary:
            summary.write(message)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("operation", choices=["review", "validate", "migrate", "deploy"])
    operation = parser.parse_args().operation
    try:
        if operation == "review":
            review()
            return 0
        verify_release()
        delivery = shared.configuration(target="production")
        print("::add-mask::" + delivery.password.replace("%", "%25"), flush=True)
        if operation == "migrate":
            shared.migrate(delivery)
        elif operation == "deploy":
            shared.deploy(delivery)
        else:
            print("Protected production release and target validated.")
        return 0
    except (ValueError, RuntimeError) as error:
        print(str(error), file=sys.stderr)
    except (subprocess.CalledProcessError, subprocess.TimeoutExpired):
        print("Production database setup failed; deployment stopped.", file=sys.stderr)
    except (HTTPError, URLError, TimeoutError):
        print("Production safety verification failed; no delivery action was permitted.", file=sys.stderr)
    return 1


if __name__ == "__main__":
    sys.exit(main())
