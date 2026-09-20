---
name: next
description: Start an empty conversation from the previous one's ranked next steps, after /clear. Takes which items to do, plus anything of your own.
argument-hint: 1,2 and anything else you want, in your own words
allowed-tools: Bash(python:*), Bash(git status:*), Bash(git log:*), Bash(gh pr list:*)
---

# Carry on from where the last conversation stopped

This conversation is empty because it follows a `/clear`. That is deliberate: a request costs
about `0.10 x context`, so the work ahead runs several times cheaper here than it would have in
the conversation that just ended. Nothing was lost that the repository does not already hold,
and `/resume` still has the old conversation if something is missing.

The previous conversation ended with a ranked list. It is below, read out of its transcript, so
it is whatever was last said rather than whatever somebody remembered to write down.

!`python "${CLAUDE_SKILL_DIR}/handover.py"`

The repository as it stands now:

```!
git status -sb
git log --oneline -3
gh pr list --limit 5
```

## What the owner wants from this session

$ARGUMENTS

## How to read all of that

Work the items named above, in the order named. **If none are named, ask which before starting
anything.** An item the owner marked as deferred stays untouched, and the owner's words outrank
the list where the two differ: the list is a proposal from the last conversation, not a plan.

The list is a starting point and not a brief. Check it against the repository before acting on
it: a pull request in it may have merged, a figure may have moved, and the age printed with the
list says how much room there is for that. Say so when it happens rather than working from the
stale reading.

One session, one subject. When the work is done and a pull request is waiting on CI, say so and
stop rather than opening the next subject here; that is what the next `/clear` is for.

If the blocks above did not expand, run `python .claude/skills/next/handover.py` with Bash and
carry on from what it prints.
