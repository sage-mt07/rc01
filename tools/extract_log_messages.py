"""Extract log messages from C# files.

The script scans the repository for calls to ILogger.Log* methods and attempts
to capture the first string literal passed as the log message. It makes a best
effort to handle multi-line statements and simple variable assignments, but it
is **not** a full C# parser. Complex cases may still produce empty messages.

Usage:
    python3 tools/extract_log_messages.py > log_messages.md

Limitations:
    - Only the first argument of Log* calls is inspected.
    - Variable assignments are searched up to five lines above the call.
    - The script does not evaluate interpolated expressions or format
      placeholders; it simply extracts the string literal.
"""

import os
import re

LOG_CALL_RE = re.compile(r"\b(Log(Debug|Information|Warning|Error|Trace|Critical))\s*\(")


def extract_message(lines, idx, start_pos, var_name=None):
    """Return the extracted message string for the call starting at idx."""

    call_text = lines[idx][start_pos:]
    j = idx
    # Collect lines until closing parenthesis
    while ')' not in call_text and j + 1 < len(lines):
        j += 1
        call_text += lines[j]

    # look for a string literal in the call text
    literal = re.search(r'"([^"\\]*(?:\\.[^"\\]*)*)"', call_text)
    if literal:
        return literal.group(1)

    # If a variable name was provided, search previous lines for assignment
    if var_name:
        for k in range(idx - 1, max(-1, idx - 6), -1):
            segment = lines[k].strip()
            if var_name in segment and '=' in segment:
                assign = segment
                l = k
                # handle multi-line assignment ending with ';'
                while ';' not in assign and l + 1 < idx:
                    l += 1
                    assign += lines[l].strip()
                match = re.search(r'"([^"\\]*(?:\\.[^"\\]*)*)"', assign)
                if match:
                    return match.group(1)
    # fallback to the variable name if nothing could be resolved
    return f'<{var_name}>' if var_name else ''


def parse_file(path):
    entries = []
    with open(path, 'r', encoding='utf-8', errors='ignore') as f:
        lines = f.readlines()
    for i, line in enumerate(lines):
        m = LOG_CALL_RE.search(line)
        if not m:
            continue
        severity = m.group(1)
        after_pos = m.end()
        # Capture the first argument token
        after = line[after_pos:]
        arg_match = re.match(r"\s*([A-Za-z_][A-Za-z0-9_]*|\")", after)
        var_name = None
        if arg_match and not arg_match.group(0).startswith('"'):
            var_name = arg_match.group(1)
        message = extract_message(lines, i, after_pos, var_name)
        entries.append((path.lstrip('./'), i + 1, severity, message))
    return entries


all_entries = []
for root, dirs, files in os.walk('.'):
    if '.git' in root:
        continue
    for fname in files:
        if fname.endswith('.cs'):
            file_entries = parse_file(os.path.join(root, fname))
            all_entries.extend(file_entries)

all_entries.sort()

print('| File | Line | Severity | Message |')
print('|------|------|----------|---------|')
for e in all_entries:
    print(f'| {e[0]} | {e[1]} | {e[2]} | {e[3]} |')
