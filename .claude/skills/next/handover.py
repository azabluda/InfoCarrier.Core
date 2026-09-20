"""Print the handover the previous conversation already wrote.

Every reply here ends with a ranked list of next steps, so the handover exists without anyone
maintaining it: it is the newest such list in this project's transcripts. Reading it out of the
transcript rather than out of a file means a session that ended at the five-hour wall, or one
that was closed without ceremony, still hands over whatever it last said.

Called by the `next` skill after /clear. Prints nothing but the list and its age.
"""
import datetime
import json
import os
import re

HEADING = re.compile(r'^\s*#*\s*ranked next steps\s*:?\s*$', re.I | re.M)
# A backslash is spelled chr(92) on purpose: this file is generated often, and a shell heredoc
# collapses a doubled backslash, which silently turned the separator class into a no-op once.
SEPARATORS = (chr(92), '/', ':', '.')


def project_dir(cwd):
    """~/.claude/projects holds one directory per project, named after its path."""
    want = cwd
    for ch in SEPARATORS:
        want = want.replace(ch, '-')
    want = want.lower()
    base = os.path.expanduser('~/.claude/projects')
    if not os.path.isdir(base):
        return None
    for name in os.listdir(base):
        if name.lower() == want:
            return os.path.join(base, name)
    return None


def blocks(path):
    """Every assistant message in a transcript that ends with a ranked list."""
    out = []
    for line in open(path, encoding='utf-8', errors='replace'):
        if 'anked next steps' not in line:
            continue
        try:
            rec = json.loads(line)
        except ValueError:
            continue
        msg = rec.get('message') or {}
        if rec.get('type') != 'assistant' or not isinstance(msg, dict):
            continue
        for part in msg.get('content') or []:
            if isinstance(part, dict) and part.get('type') == 'text':
                m = HEADING.search(part.get('text', ''))
                if m:
                    out.append((rec.get('timestamp', ''), os.path.basename(path)[:8],
                                part['text'][m.end():].strip()))
    return out


def main():
    cwd = os.getcwd()
    d = project_dir(cwd)
    if not d:
        print('No transcripts found for %s. Tell me what to work on.' % cwd)
        return
    found = []
    for fn in os.listdir(d):
        if fn.endswith('.jsonl'):
            found += blocks(os.path.join(d, fn))
    if not found:
        print('No previous next steps found. Tell me what to work on.')
        return
    ts, sess, text = max(found)
    try:
        when = datetime.datetime.fromisoformat(ts.replace('Z', '+00:00')).astimezone()
        age = (datetime.datetime.now(when.tzinfo) - when).total_seconds() / 3600
        stamp = '%s, session %s, %.0f hours ago' % (when.strftime('%Y-%m-%d %H:%M'), sess, age)
    except ValueError:
        stamp = 'session ' + sess
    print('The previous conversation ended with this list (%s):' % stamp)
    print()
    print(text)


if __name__ == '__main__':
    main()
