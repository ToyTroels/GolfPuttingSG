import io
import json
from pathlib import Path
import sys
import unittest
from unittest.mock import patch
from urllib.parse import parse_qs, urlsplit

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import deliver_staging as delivery


class StagingDeliveryTests(unittest.TestCase):
    def setUp(self):
        self.metadata = json.loads((delivery.ROOT / "deploy/staging.json").read_text(encoding="utf-8"))
        self.sha = "0123456789abcdef0123456789abcdef01234567"
        self.environment = {
            "GITHUB_SHA": self.sha,
            "GITHUB_ACTIONS": "true",
            "GITHUB_REF": "refs/heads/dev",
            "GITHUB_EVENT_NAME": "push",
            "STAGING_RENDER_DEPLOY_HOOK": "https://api.render.com/deploy/" + self.metadata["render"]["serviceId"] + "?key=test-only-key&ref=old",
            "STAGING_DATABASE_SETUP_CONNECTION": "postgresql://golfsg_staging_owner:synthetic-password@" + self.metadata["neon"]["directHost"] + "/golfsg_staging?sslmode=require",
        }

    def configuration(self):
        return delivery.configuration(self.environment, self.metadata)

    def test_hook_is_pinned_to_the_validated_commit(self):
        settings = self.configuration()
        self.assertEqual([self.sha], parse_qs(urlsplit(settings.hook).query)["ref"])
        self.assertIn("Username=\"golfsg_staging_owner\"", settings.database_connection)
        self.assertNotIn("-pooler", settings.database_connection)

    def test_missing_secret_or_wrong_branch_stops_before_database_setup(self):
        for key, value in [("STAGING_RENDER_DEPLOY_HOOK", ""), ("STAGING_DATABASE_SETUP_CONNECTION", ""),
                           ("GITHUB_REF", "refs/heads/master"), ("GITHUB_EVENT_NAME", "pull_request")]:
            with self.subTest(key=key):
                invalid = dict(self.environment, **{key: value})
                with self.assertRaises(ValueError):
                    delivery.configuration(invalid, self.metadata)

    def test_production_hook_and_unpooled_runtime_credentials_are_rejected(self):
        invalid = dict(self.environment, STAGING_RENDER_DEPLOY_HOOK="https://api.render.com/deploy/srv-production?key=test")
        with self.assertRaises(ValueError):
            delivery.configuration(invalid, self.metadata)
        for old, new in [(self.metadata["neon"]["directHost"], "production.example.invalid"),
                         (self.metadata["neon"]["directHost"], self.metadata["neon"]["directHost"].replace(".c-", "-pooler.c-")),
                         ("golfsg_staging_owner", "golfsg_staging_app"),
                         ("/golfsg_staging?", "/production?"), ("sslmode=require", "sslmode=disable")]:
            with self.subTest(replacement=new):
                invalid = dict(self.environment)
                invalid["STAGING_DATABASE_SETUP_CONNECTION"] = invalid["STAGING_DATABASE_SETUP_CONNECTION"].replace(old, new)
                with self.assertRaises(ValueError):
                    delivery.configuration(invalid, self.metadata)

    def test_ready_old_deployment_does_not_count_as_success(self):
        with patch.object(delivery, "read_json", side_effect=[{"commit": "f" * 40}, {"databaseAvailable": True}]):
            self.assertFalse(delivery.serving_commit(self.configuration()))

    def test_matching_commit_with_failed_readiness_does_not_count_as_success(self):
        with patch.object(delivery, "read_json", side_effect=[{"commit": self.sha}, {"databaseAvailable": False}]):
            self.assertFalse(delivery.serving_commit(self.configuration()))

    def test_exact_commit_and_readiness_are_required(self):
        with patch.object(delivery, "read_json", side_effect=[{"commit": self.sha}, {"databaseAvailable": True}]):
            self.assertTrue(delivery.serving_commit(self.configuration()))

    def test_database_owner_connection_is_passed_only_as_environment(self):
        settings = self.configuration()
        with patch.object(delivery.subprocess, "run") as run:
            delivery.migrate(settings)
        restore, migrate = run.call_args_list
        self.assertNotIn(settings.password, str(restore))
        self.assertNotIn(settings.password, str(migrate.args))
        self.assertEqual(settings.database_connection, migrate.kwargs["env"]["ConnectionStrings__DatabaseSetup"])
        self.assertEqual("golfsg_staging_app", migrate.kwargs["env"]["DatabaseSetup__ApplicationRole"])

    def test_database_failure_stops_before_deployment(self):
        with patch.object(delivery.subprocess, "run", side_effect=delivery.subprocess.CalledProcessError(1, ["dotnet"])):
            with self.assertRaises(delivery.subprocess.CalledProcessError):
                delivery.migrate(self.configuration())

    def test_already_deployed_commit_does_not_trigger_a_second_deploy(self):
        with patch.object(delivery, "serving_commit", return_value=True), patch.object(delivery, "urlopen") as request:
            delivery.deploy(self.configuration())
        request.assert_not_called()

    def test_timeout_does_not_accept_a_healthy_old_revision(self):
        response = io.BytesIO(b"{}")
        with patch.object(delivery, "serving_commit", return_value=False), \
             patch.object(delivery, "urlopen", return_value=response), \
             patch.object(delivery.time, "monotonic", side_effect=[0, 0, 11]), \
             patch.object(delivery.time, "sleep"):
            with self.assertRaises(RuntimeError):
                delivery.deploy(self.configuration(), timeout_seconds=10)


if __name__ == "__main__":
    unittest.main()
