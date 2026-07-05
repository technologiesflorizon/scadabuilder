import contextlib
import io
import os
import tempfile
import unittest

from icon_modernization.cli import run_check_junctions

_MATCHING_ORIGINAL = (
    '<svg xmlns="http://www.w3.org/2000/svg" width="100" height="10">'
    '<line x1="0" y1="5" x2="100" y2="5"/></svg>'
)
_MATCHING_CANDIDATE = (
    '<svg xmlns="http://www.w3.org/2000/svg" width="100" height="10">'
    '<line x1="0" y1="4" x2="100" y2="4"/></svg>'
)
_MISMATCHED_CANDIDATE = (
    '<svg xmlns="http://www.w3.org/2000/svg" width="100" height="10">'
    '<line x1="0" y1="9" x2="100" y2="9"/></svg>'
)


def _write_temp_svg(directory: str, name: str, markup: str) -> str:
    path = os.path.join(directory, name)
    with open(path, "w", encoding="utf-8") as f:
        f.write(markup)
    return path


class TestRunCheckJunctions(unittest.TestCase):
    def test_matching_geometry_within_tolerance_returns_zero(self):
        with tempfile.TemporaryDirectory() as tmp:
            original = _write_temp_svg(tmp, "original.svg", _MATCHING_ORIGINAL)
            candidate = _write_temp_svg(tmp, "candidate.svg", _MATCHING_CANDIDATE)
            buffer = io.StringIO()
            with contextlib.redirect_stdout(buffer):
                exit_code = run_check_junctions(original, candidate, tolerance_px=2.0)
            self.assertEqual(exit_code, 0)
            self.assertIn("OK", buffer.getvalue())

    def test_mismatched_geometry_beyond_tolerance_returns_one(self):
        with tempfile.TemporaryDirectory() as tmp:
            original = _write_temp_svg(tmp, "original.svg", _MATCHING_ORIGINAL)
            candidate = _write_temp_svg(tmp, "candidate.svg", _MISMATCHED_CANDIDATE)
            buffer = io.StringIO()
            with contextlib.redirect_stdout(buffer):
                exit_code = run_check_junctions(original, candidate, tolerance_px=2.0)
            self.assertEqual(exit_code, 1)
            self.assertIn("FAIL", buffer.getvalue())
            self.assertIn("MISSING", buffer.getvalue())


if __name__ == "__main__":
    unittest.main()
