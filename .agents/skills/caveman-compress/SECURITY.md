# Security

## Snyk High Risk Rating

`caveman-compress` receives a Snyk High Risk rating due to static analysis heuristics. This document explains what the skill does and does not do.

### What triggers the rating

1. **subprocess usage**: The skill calls the `claude` CLI via `subprocess.run()` as a fallback when `ANTHROPIC_API_KEY` is not set. The subprocess call uses a fixed argument list — no shell interpolation occurs. User file content is passed via stdin, not as a shell argument.

2. **File read/write**: The skill reads the file the user explicitly points it at, compresses it, and writes the result back to the same path. A `.original.md` backup is written outside the source directory under the directory returned by `backup_dir_for()`: `%LOCALAPPDATA%\caveman-compress\backups\<source-parent>` on Windows, or `~/AppData/Local\caveman-compress\backups\<source-parent>` if `%LOCALAPPDATA%` is unset; `$XDG_DATA_HOME/caveman-compress/backups/<source-parent>` or `~/.local/share/caveman-compress/backups/<source-parent>` elsewhere. The backup filename is `<stem>.original.md`, so the full backup shape is `<platform-data>/caveman-compress/backups/<source-parent>/<stem>.original.md`. This avoids skill auto-loaders re-ingesting backup copies as live files.

### What the skill does NOT do

- Does not execute user file content as code
- Does not make network requests except to Anthropic's API (via SDK or CLI)
- Does not read additional input files outside the path the user provides; it only reads back its own backup file to verify backup integrity before replacing the source
- Does write backup files to the platform data directory described above
- Does not use shell=True or string interpolation in subprocess calls
- Does not collect or transmit any data beyond the file being compressed

### Auth behavior

If `ANTHROPIC_API_KEY` is set, the skill uses the Anthropic Python SDK directly (no subprocess). If not set, it falls back to the `claude` CLI, which uses the user's existing Claude desktop authentication.

### File size limit

Files larger than 500KB are rejected before any API call is made.

### Reporting a vulnerability

If you believe you've found a genuine security issue, please open a GitHub issue with the label `security`.
