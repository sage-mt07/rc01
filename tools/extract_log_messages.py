import re
import os

LOG_PATTERN = re.compile(r"\b(Log(Debug|Information|Warning|Error|Trace|Critical))\s*\(")

entries = []
for root, dirs, files in os.walk('.'):
    if '.git' in root:
        continue
    for fname in files:
        if fname.endswith('.cs'):
            path = os.path.join(root, fname)
            with open(path, 'r', encoding='utf-8', errors='ignore') as f:
                for i, line in enumerate(f, 1):
                    m = LOG_PATTERN.search(line)
                    if m:
                        severity = m.group(1)
                        # attempt to get message within the quotes
                        after = line[m.end():]
                        # naive parse: read until next quote
                        msg_match = re.search(r'"([^"\\]*(?:\\.[^"\\]*)*)"', after)
                        message = msg_match.group(1) if msg_match else ''
                        entries.append((path.lstrip('./'), i, severity, message))

entries.sort()

print('| File | Line | Severity | Message |')
print('|------|------|----------|---------|')
for e in entries:
    print(f'| {e[0]} | {e[1]} | {e[2]} | {e[3]} |')
