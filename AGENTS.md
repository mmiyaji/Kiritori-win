# Repository Agent Notes

## Approval Policy

- Git operations may proceed without confirmation except for `git push` and `git merge`.
- `git reset --hard` still requires confirmation.
- `Remove-Item` may proceed without confirmation when the target is clearly inside this workspace and scoped to the current task.
- Prefer file-scoped Git operations such as `git restore --source=HEAD -- <file>` over broad restore/reset commands.
