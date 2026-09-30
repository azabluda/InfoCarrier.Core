# Codex Repository Configuration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move shareable Claude Code customizations into trusted, version-controlled Codex repository configuration.

**Architecture:** Repository instructions stay in `AGENTS.md`. Codex configuration, hooks, and command rules live under `.codex/`; shared skills live under `.agents/skills/`. Hook scripts consume Codex event payloads and remain scoped to this repository.

**Tech Stack:** Codex configuration, JSON, TOML, Starlark command rules, Python 3, `unittest`

**Spec:** The approved migration review in this session, plus `.claude/settings.json`, `.mcp.json`, and `.claude/skills/`.

## Global Constraints

- Preserve personal model and reasoning settings in user configuration only.
- Keep `.codex-migration/` unchanged until the owner approves removal.
- Never edit files under `subrepos/**`.
- Use LF line endings and no UTF-8 byte-order mark in scripts and configuration.
- Do not commit changes without a separate owner request.

## Review Focus

- A patch can contain several files; hygiene must inspect every changed path.
- Windows patch paths can use either slash style.
- Deleted files must not cause hygiene failures.
- Hooks must do nothing outside this repository.
- Command rules cannot replace path-based edit protection.

---

### Task 1: Repository hooks

**Files:**
- Create: `.codex/tests/test_hooks.py`
- Create: `.codex/hooks/cs-search-reminder.py`
- Create: `.codex/hooks/file-hygiene.py`
- Create: `.codex/hooks.json`

**Interfaces:**
- Consumes: Codex hook JSON on standard input.
- Produces: `PreToolUse` additional context and `PostToolUse` hygiene feedback.

- [x] Write hook behavior tests covering reminders, multi-file patches, path separators, deletions, clean files, violations, and unrelated repositories.
- [x] Run the hook tests and confirm missing implementations fail.
- [x] Implement the minimal Codex payload adapters and repository-local hook registration.
- [x] Run the hook tests and confirm they pass.

### Task 2: Shared skills

**Files:**
- Create: `.agents/skills/experiment/SKILL.md`
- Create: `.agents/skills/handoff/SKILL.md`

**Interfaces:**
- Consumes: Explicit or implicit Codex skill selection.
- Produces: The approved experiment and handoff workflows.

- [x] Run one baseline scenario for each skill without repository skill discovery.
- [x] Copy the approved Codex adaptations into `.agents/skills/`.
- [x] Validate each skill with `quick_validate.py`.
- [x] Run the same scenarios with each repository skill and confirm compliant behavior.

### Task 3: Project configuration and command rules

**Files:**
- Create: `.codex/config.toml`
- Create: `.codex/rules/repository.rules`
- Create: `AGENTS.md`

**Interfaces:**
- Consumes: Trusted project configuration layers.
- Produces: Roslyn CodeLens registration, shared command allowances, and repository instructions.

- [x] Add only repository-portable settings and the Roslyn CodeLens server to `.codex/config.toml`.
- [x] Translate safe command prefixes into `.codex/rules/repository.rules` with match tests.
- [x] Add repository instructions to `AGENTS.md` without personal model settings.
- [x] Run `codex execpolicy check` against allowed and unrelated command examples.

### Task 4: Integrated verification

**Files:**
- Verify all files created by Tasks 1 through 3.

**Interfaces:**
- Consumes: The completed repository configuration.
- Produces: Evidence that teammates can discover and trust the same configuration.

- [x] Run hook unit tests and both skill validators.
- [x] Parse `hooks.json`, `config.toml`, and the rule file with their supported tools.
- [x] Run the repository hygiene hook against every changed file.
- [x] Confirm `.codex-migration/` remains present and unchanged.
- [x] Report the trust steps that each teammate must complete.
