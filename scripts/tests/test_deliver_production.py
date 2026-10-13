import json
from pathlib import Path
import sys
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import deliver_production as production
import deliver_staging as shared


class ProductionDeliveryTests(unittest.TestCase):
    def setUp(self):
        self.settings = production.metadata()
        self.sha = "a" * 40
        self.staged_sha = "b" * 40
        self.tree = "c" * 40
        self.environment = {"GITHUB_ACTIONS": "true", "GITHUB_EVENT_NAME": "push",
                            "GITHUB_REF": "refs/heads/master", "GITHUB_SHA": self.sha,
                            "GITHUB_REPOSITORY": "ToyTroels/GolfPuttingSG"}
        self.protection = {
            "can_admins_bypass": False,
            "protection_rules": [{"type": "required_reviewers", "reviewers": [
                {"type": "User", "reviewer": {"login": "ToyTroels"}}]}],
            "deployment_branch_policy": {"custom_branch_policies": True, "protected_branches": False},
        }
        self.rules = {"branch_policies": [{"name": "master", "type": "branch"}]}
        self.head = {"object": {"sha": self.sha}}
        self.deployment = {"commit": self.staged_sha}
        self.health = {"databaseAvailable": True}

    def verify(self):
        with patch.object(production, "github_json", side_effect=[self.protection, self.rules, self.head,
                          {"tree": {"sha": self.tree}}]), \
             patch.object(shared, "read_json", side_effect=[self.deployment, self.health]), \
             patch.object(production.subprocess, "check_output", side_effect=[self.tree + "\n", self.sha + "\n"]):
            return production.verify_release(self.environment, self.settings)

    def test_current_master_with_identical_healthy_staging_and_manual_gate_is_ready(self):
        self.assertEqual((self.sha, self.staged_sha), self.verify())

    def test_wrong_branch_pull_request_repository_or_local_run_cannot_deliver(self):
        for key, value in [("GITHUB_REF", "refs/heads/dev"), ("GITHUB_EVENT_NAME", "pull_request"),
                           ("GITHUB_REPOSITORY", "another/repo"), ("GITHUB_ACTIONS", "false")]:
            with self.subTest(key=key):
                original = self.environment[key]
                self.environment[key] = value
                with self.assertRaises(ValueError):
                    self.verify()
                self.environment[key] = original

    def test_missing_reviewer_different_reviewer_or_admin_bypass_fails_closed(self):
        for rules, bypass in [([], False), ([{"type": "required_reviewers", "reviewers": [
                                {"reviewer": {"login": "somebody-else"}}]}], False),
                              (self.protection["protection_rules"], True)]:
            with self.subTest(bypass=bypass, rules=rules):
                self.protection["protection_rules"] = rules
                self.protection["can_admins_bypass"] = bypass
                with self.assertRaises(ValueError):
                    self.verify()

    def test_wildcard_empty_or_tag_deployment_rules_cannot_access_production(self):
        for rules in [[], [{"name": "*", "type": "branch"}], [{"name": "master", "type": "tag"}],
                      [{"name": "master", "type": "branch"}, {"name": "dev", "type": "branch"}]]:
            self.rules["branch_policies"] = rules
            with self.subTest(rules=rules), self.assertRaises(ValueError):
                self.verify()

    def test_new_master_commit_invalidates_an_older_queued_release(self):
        self.head = {"object": {"sha": "d" * 40}}
        with self.assertRaises(ValueError):
            self.verify()

    def test_unhealthy_or_unidentified_staging_cannot_be_promoted(self):
        self.health = {"databaseAvailable": False}
        with self.assertRaises(ValueError):
            self.verify()
        self.health = {"databaseAvailable": True}
        self.deployment = {"commit": None}
        with self.assertRaises(ValueError):
            self.verify()

    def test_different_source_tree_cannot_be_promoted(self):
        self.tree = "d" * 40
        with patch.object(production, "github_json", side_effect=[self.protection, self.rules, self.head,
                          {"tree": {"sha": "c" * 40}}]), \
             patch.object(shared, "read_json", side_effect=[self.deployment, self.health]), \
             patch.object(production.subprocess, "check_output", side_effect=[self.tree, self.sha]), \
             self.assertRaises(ValueError):
            production.verify_release(self.environment, self.settings)

    def test_production_secrets_reject_staging_hook_or_database(self):
        environment = dict(self.environment,
            PRODUCTION_RENDER_DEPLOY_HOOK="https://api.render.com/deploy/" + self.settings["render"]["serviceId"] + "?key=synthetic",
            PRODUCTION_DATABASE_SETUP_CONNECTION="postgresql://neondb_owner:synthetic@" + self.settings["neon"]["directHost"] + "/neondb?sslmode=require")
        configured = shared.configuration(environment, self.settings, target="production")
        self.assertEqual("Production", configured.name)
        self.assertIn("ref=" + self.sha, configured.hook)
        for key, value in [("PRODUCTION_RENDER_DEPLOY_HOOK", "https://api.render.com/deploy/srv-db754sff3r2c73d82rg0?key=synthetic"),
                           ("PRODUCTION_DATABASE_SETUP_CONNECTION", environment["PRODUCTION_DATABASE_SETUP_CONNECTION"].replace("/neondb?", "/golfsg_staging?"))]:
            with self.subTest(key=key), self.assertRaises(ValueError):
                shared.configuration(dict(environment, **{key: value}), self.settings, target="production")

    def test_failed_safety_check_never_reaches_migration_or_deploy(self):
        with patch.object(sys, "argv", ["deliver_production.py", "migrate"]), \
             patch.object(production, "verify_release", side_effect=ValueError("Missing approval protection")), \
             patch.object(shared, "migrate") as migrate, patch.object(shared, "deploy") as deploy:
            self.assertEqual(1, production.main())
        migrate.assert_not_called()
        deploy.assert_not_called()


if __name__ == "__main__":
    unittest.main()
