# AGENTS.md (MESSimulator)

Instructions for any coding agent working in `Tools/mes-simulators` (Claude Code, GitHub Copilot, Codex, Cursor and
others).

## What this is

A .NET 8 console app that drives lots through a Critical Manufacturing MES flow until production orders close.
[README.md](README.md) explains how to run it and is the **build journal**: every MES quirk and every error met so
far, with its fix. Read it before changing anything, and add to it whenever you learn something new.

## Skills: read the one that fits before you start

The skills follow the Agent Skills format (`SKILL.md`). Tools that support skills load them automatically. Other
tools must read the file at the path below.

| Task | Skill |
|---|---|
| Build, extend or debug the simulator against an MES; add a step or action; find why a run stopped | [.claude/skills/build-mes-simulator/SKILL.md](.claude/skills/build-mes-simulator/SKILL.md) |
| Derive `simulationConfiguration.json` and a step plan from master data files (`master data/`), without an MES | [.claude/skills/simulator-from-masterdata/SKILL.md](.claude/skills/simulator-from-masterdata/SKILL.md) |

There is also an agent that uses both skills (Claude Code reads the first file, Copilot the second, which points to the first):
[.claude/agents/mes-simulator-builder.md](.claude/agents/mes-simulator-builder.md) and [.github/agents/mes-simulator-builder.agent.md](.github/agents/mes-simulator-builder.agent.md). Claude Code and VS Code
Copilot pick it up when this folder is the workspace.

## Rules that always apply

- Only the **Core** flow features are simulated. Engineering and Quality features are out of scope unless asked.
- The MES routes the lot: never hardcode the step order. Steps are configured by MES step name in `simulationConfiguration.json`.
- If a step behaves differently from similar steps, flag it to the user. Never normalize the difference.
- Only touch MES data the simulator created. Never select production orders by name prefix alone.
- Never print or commit the token in `appsettings.json`, or `.har` captures.
- The line file is required: run with `--line simulationConfiguration.semi.json` (or `.industrial.json`, or
  `MESSIM_LINE_FILE`). Before a live run against an MES you don't own, run `--dry-run` first: it only lists what the
  startup would change. Integration tests only run with `MESSIM_INTEGRATION=1`.
- Validate in order: `dotnet build` → `dotnet test Tests/MESSimulator.UnitTests` → live run → production orders
  `Closed`. When you report, say which of these you ran.
- Don't commit: the user does.
