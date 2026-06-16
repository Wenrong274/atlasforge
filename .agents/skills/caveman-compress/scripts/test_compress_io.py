import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch

from scripts import compress


class CompressIoTests(unittest.TestCase):
    def test_text_helpers_round_trip_utf8_characters(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "memory.md"
            text = "Heading\nemoji 😀\nCJK 測試\naccent café\n"

            compress.write_text_utf8(path, text)

            self.assertEqual(text, compress.read_text_utf8(path))

    def test_compress_file_preserves_utf8_backup(self):
        with tempfile.TemporaryDirectory() as tmp:
            tmp_path = Path(tmp)
            source = tmp_path / "memory.md"
            backup_dir = tmp_path / "backups"
            source.write_text("Heading\nemoji 😀\nCJK 測試\n", encoding="utf-8")

            with (
                patch.object(compress, "should_compress", return_value=True),
                patch.object(compress, "backup_dir_for", return_value=backup_dir),
                patch.object(compress, "call_claude", return_value="Compressed 😀 測試"),
                patch.object(
                    compress,
                    "validate",
                    return_value=SimpleNamespace(is_valid=True, errors=[]),
                ),
            ):
                self.assertTrue(compress.compress_file(source))

            backup = backup_dir / "memory.original.md"
            self.assertEqual("Heading\nemoji 😀\nCJK 測試\n", backup.read_text(encoding="utf-8"))
            self.assertEqual("Compressed 😀 測試", source.read_text(encoding="utf-8"))
