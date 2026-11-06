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
- Only change code required by the current task. Never edit, refactor, reformat, or delete code outside the task scope.
- Never commit or push. Only the human user commits and pushes.
- Never open a pull request unless explicitly asked.
- Never rewrite history or discard uncommitted user changes.
- Ask before adding dependencies, changing public APIs, or modifying protected branches.
- When a task is complete, report changed files, verification performed, and any follow-up.
