import re
import os
from collections import defaultdict

# Patterns used to map file paths to logical categories.
CATEGORY_PATTERNS = [
    (os.path.join('Kafka.Ksql.Linq.Importer'), 'Importer'),
    ('physicalTests', 'Tests'),
]


def get_category(path: str) -> str:
    """Return a category name derived from the given file path."""
    path = path.lstrip('./\\')
    for pattern, cat in CATEGORY_PATTERNS:
        if pattern in path:
            return cat

    if path.startswith('src' + os.sep):
        parts = path.split(os.sep)
        if len(parts) >= 2:
            segment = parts[1]
            if '.' in segment:  # file directly under src
                return os.path.splitext(segment)[0]
            return segment

    return 'Misc'


def get_abbr(category: str) -> str:
    """Return a three letter abbreviation for the category."""
    alnum = ''.join(c for c in category if c.isalnum())
    return alnum[:3].upper().ljust(3, 'X')

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
                        category = get_category(path)
                        entries.append(
                            (path.lstrip('./'), i, severity, message, category)
                        )

entries.sort()

category_counts = defaultdict(int)

print('| File | Line | Severity | Message | Category | LogID |')
print('|------|------|----------|---------|----------|-------|')
for path, line_no, severity, message, category in entries:
    category_counts[category] += 1
    log_id = f"{get_abbr(category)}-{category_counts[category]:03d}"
    print(f'| {path} | {line_no} | {severity} | {message} | {category} | {log_id} |')
