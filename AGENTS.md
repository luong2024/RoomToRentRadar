# AGENTS.md

Project-wide rules for AI agents and human contributors. If a nested `AGENTS.md` exists, it overrides these rules for its directory.

## Repository layout

- `RoomToRentRadar/` — .NET MAUI app (Android, iOS, Mac Catalyst, Windows)
- `RoomToRentRadarUnitTests/` — NUnit unit tests
- `RoomToRentRadarUIAppiumTests/` — NUnit Appium UI tests
- `.agents/skills/` — canonical skills shared by all tools (also auto-discovered by Codex)
- `.claude/skills/`, `.claude/agents/` — Claude Code mirrors
- `.opencode/agent/` — opencode subagents
- `.codex/agents/` — Codex custom subagents
- `.qoder/skills/`, `.qoder/agents/` — Qoder IDE/CLI mirrors
- `opencode.json` — opencode configuration

## Git rules

### Never commit to main

- `main` is protected. Never commit, merge, or push directly to `main`.
- Never force-push `main` or any shared branch.
- All changes go through a feature branch and a pull request.

### Branch naming

Use `<type>/<short-kebab-description>` with an optional ticket ID: `<type>/<ticket>-<description>`.

Allowed types: `feat`, `fix`, `refactor`, `test`, `docs`, `chore`, `perf`, `ci`.

- `feat/maui-login-screen`
- `fix/collectionview-scroll`
- `docs/agents-rules`

Rules: lowercase, hyphen-separated, max 50 characters, one branch per change.

### Starting work

1. `git switch main && git pull --ff-only`
2. `git switch -c <branch>`
3. Keep the branch current by rebasing on `main`. Never merge `main` into the branch.

### Commits

- Follow Conventional Commits: `type(scope): summary`.
- Use imperative mood, lowercase summary, no trailing period, max 72 characters.
- One logical change per commit; do not mix refactoring with behavior changes.
- Explain why in the body. Reference issues with `Refs #123`.
- Never commit secrets, local config, build output, or generated files.

### Pull requests

- Target `main`. The title follows Conventional Commits.
- Description must cover: what changed, why, how it was tested, screenshots for UI changes, and linked issue.
- Keep PRs small and focused; split unrelated changes into separate PRs.
- CI must pass and review comments must be resolved.
- At least one approval is required. Do not self-merge without approval.
- Squash merge and delete the branch after merging.
- Do not force-push after review without notifying reviewers.

## Code quality

- `dotnet build` must succeed with no new warnings.
- Run the relevant tests before pushing; add tests for behavior changes.
- Unit and UI tests use NUnit; follow the existing conventions in the test projects.
- Follow existing conventions and project structure.
- Do not add NuGet packages without justification.
- Keep XAML and ViewModels consistent with the installed MAUI skills.

## Security

- Never commit secrets, API keys, tokens, connection strings, or personal data.
- Use user-secrets or environment variables for local configuration.
- Do not log sensitive data.
- Report security issues privately, not in public issues or PRs.

## Agent rules

- Read the relevant `SKILL.md` under `.agents/skills/` before working in an area it covers.
- Delegate to the owning subagent: `maui-ui`, `maui-data`, `dotnet-api`, `maui-tester`, `maui-reviewer`.
- Do not commit, push, or open a pull request unless explicitly asked.
- Never rewrite history or discard uncommitted user changes.
- Ask before adding dependencies, changing public APIs, or modifying protected branches.
- When a task is complete, report changed files, verification performed, and any follow-up.
