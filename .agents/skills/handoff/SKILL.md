---
name: handoff
description: Use when ending a session and preparing a copy-paste handoff from the latest ranked next steps.
---

# Hand this session over

The owner will paste the output into a new session. Print the handoff and do nothing else.
Do not edit files, commit, or start any listed work.

Use the last ranked list already posted in the conversation. Before composing the handoff, run:

```powershell
git branch --show-current
gh pr list --state open --limit 10
```

Optional numbers in the request select items from the latest ranked list. They are not pull
request numbers.

## Output

Print one line identifying the handoff. Then print one fenced block opened with four backticks
and the word `markdown`. Print nothing after that block.

Inside the block, use this order:

1. `# Handoff, <today's date>` and one sentence describing this session.
2. `How to use this`, with these three instructions:
   - verify each item against the repository before acting;
   - if named items are complete, identify their commit or pull request and stop;
   - keep one subject per session and stop when completed work waits for continuous integration.
3. `Where things stand`, covering each open pull request and what it waits for.
4. `Decisions`, containing owner decisions that the repository does not record.
5. `Ranked next steps`, preserving the latest list's full context and current status.
6. End with the requested item numbers, or `Owner: say which items to take.`

Short means no extra material. It does not mean shortening required context. If no ranked list
exists, say so in one line instead of inventing one.
