---
name: handoff
description: Print a copy-paste handoff for a new session, built from this session's own latest ranked next steps. Run it in the session that is ending, not the new one.
argument-hint: optional - which items to carry, or a note of your own
allowed-tools: Bash(git branch:*), Bash(git status:*), Bash(gh pr list:*)
---

# Hand this session over

The owner will copy what you print and paste it into a NEW session, which starts with none of this
conversation. **Print it and do nothing else**: no edits, no commits, no work on any item.

You already hold the ranked list: it is the last one you posted in this conversation. Do not look
for it anywhere else. The only thing to check is what may have moved since you posted it:

```!
git branch --show-current
gh pr list --state open --limit 10
```

## What to print

One line saying the block below is the handoff, then **one fenced block opened with four
backticks and the word `markdown`**, so fences inside it survive the copy. Nothing after it.
Inside the block, in this order:

1. `# Handoff, <today's date>` and one sentence saying what this session was about.
2. **How to use this**, three lines, for the session that receives it:
   - check each item against the repository before acting, because a pull request may have merged
     or a figure moved since this was written;
   - if the items the owner names are already done, say which commit or PR did each and stop,
     without looking for nearby work to fill the session;
   - one subject per session: when the work is done and a PR waits on CI, say so and stop.
3. **Where things stand**: each open PR from the block above, with what it is and whether it waits
   on CI, on the owner, or on nothing. Merged work only where the next session needs it.
4. **Decisions the owner made here that the repository does not record.** The new session cannot
   read this conversation, so a decision that lives only here will be proposed again. Give each one
   in the owner's terms, with what it rules out.
5. **Ranked next steps**: your latest list, as you posted it. Where the session has overtaken an
   item since (done, merged, rejected, parked), say so or drop it; do not repeat stale state. Keep
   every item's full context, because the new session reads only this.
6. One closing line: the items named in the arguments below, or else
   `Owner: say which items to take.`

$ARGUMENTS

**Short means nothing beyond what the list needs, not shorter items.** A new session already has
CLAUDE.md, the memory files and the repository, so repeat none of them. If you have posted no
ranked list in this conversation, say so in one line instead of inventing one.
