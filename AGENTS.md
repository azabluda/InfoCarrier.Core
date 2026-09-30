# Owner: Alexander Zabluda (`azabluda`)

## Communication

- Use ASD-STE100 Simplified Technical English in replies.
- Keep each sentence under 20 words.
- Use no idioms, metaphors, em dashes, or bold emphasis.
- Explain each short name when first used.
- Documents, commits, and pull-request text use normal English.
- End each work iteration with ranked next steps and enough decision context.
- Link every issue and pull-request number to GitHub.

## Standing rules

- Never file an issue or defect report unless the owner asks.
- Never propose or create a release unless the owner starts that work.
- Never create a worktree or second clone unless the owner asks.
- After a pull request merges, delete its local branch and run `git fetch --prune`.
- Add `Assisted by OpenAI Codex.` to every pull request.
- Do not add a Codex co-author trailer to commits.
- Keep one subject per session. Start a new session instead of compacting a long session.

## Repository instructions

- Read `CLAUDE.md` fully at the start of each repository session.
- Read `.claude/roslyn-codelens.md` fully at the same time.
- Treat Claude tool names in those files as their Codex equivalents.
- Never edit a file under `subrepos/**`.
- Use `roslyn-codelens` for C# symbol questions. Do not use text search.
- If `roslyn-codelens` closes its connection, report it and stop.
- Run `.claude/hooks/file-hygiene.py` against changed files before each commit.
- Use the `humanizer` skill before editing README, `src/*/PACKAGE.md`, or `website/`.
