from pathlib import Path
import sys
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import api_changes
import deliver_production as production


class ApiChangesTests(unittest.TestCase):
    def test_api_and_shared_delivery_inputs_trigger(self):
        for path in ["GolfSG.Api/Program.cs", "src/GolfSG.Core/Round.cs",
                     "src/GolfSG.Application/Rounds.cs", "src/GolfSG.Server.Infrastructure/Store.cs",
                     "src/GolfSG.Database.Setup/Sql/Migrations/004.sql", "Dockerfile",
                     "global.json", "Directory.Build.props", "build/AdminAccess.cs", "deploy/staging.json",
                     "scripts/deliver_staging.py", ".github/workflows/ci.yml"]:
            with self.subTest(path=path):
                self.assertTrue(api_changes.affects_api(path))

    def test_app_docs_and_app_release_workflow_do_not_trigger(self):
        for path in ["src/GolfSG/MainPage.xaml", "README.md", "docs/STAGING_DELIVERY.md",
                     "tests/GolfSG.Tests/RoundTests.cs", ".github/workflows/release-android.yml"]:
            with self.subTest(path=path):
                self.assertFalse(api_changes.affects_api(path))

    def test_push_diff_includes_both_sides_of_renames_and_all_commits(self):
        with patch.object(api_changes.subprocess, "run") as run, \
             patch.object(api_changes.subprocess, "check_output", return_value=b"README.md\0GolfSG.Api/Old.cs\0") as diff:
            run.return_value.returncode = 0
            self.assertTrue(api_changes.changed_api("a" * 40, "b" * 40))
            self.assertIn("--no-renames", diff.call_args.args[0])
            self.assertEqual(["a" * 40, "b" * 40], diff.call_args.args[0][-2:])

    def test_missing_base_requires_delivery(self):
        self.assertTrue(api_changes.changed_api("0" * 40, "b" * 40))
        with patch.object(api_changes.subprocess, "run") as run:
            run.return_value.returncode = 1
            self.assertTrue(api_changes.changed_api("a" * 40, "b" * 40))

    def test_staging_comparison_ignores_app_but_rejects_api_changes(self):
        staged = {"truncated": False, "tree": [
            {"path": "GolfSG.Api/Program.cs", "type": "blob", "mode": "100644", "sha": "abc"},
            {"path": "src/GolfSG/MainPage.xaml", "type": "blob", "mode": "100644", "sha": "old"},
        ]}
        for sha, expected in [("abc", True), ("def", False)]:
            local = f"100644 blob {sha}\tGolfSG.Api/Program.cs\0".encode() + b"100644 blob new\tsrc/GolfSG/MainPage.xaml\0"
            with patch.object(production, "github_json", return_value=staged), \
                 patch.object(production.subprocess, "check_output", return_value=local):
                self.assertEqual(expected, production.api_tree_matches("/repo", "tree", {}))

    def test_incomplete_staging_tree_fails_closed(self):
        with patch.object(production, "github_json", return_value={"truncated": True}), self.assertRaises(ValueError):
            production.api_tree_matches("/repo", "tree", {})
