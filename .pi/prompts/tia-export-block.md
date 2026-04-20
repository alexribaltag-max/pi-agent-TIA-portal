---
description: Export a PLC block using the safest or smartest strategy.
---
Use the `tia-portal-agent` skill.

Export this PLC block:
- device reference: $1
- block reference: $2
- target path or directory: $3
- extra user instructions: ${@:4}

Default behavior:
- prefer `EXPORTPLCBLOCKSMART`
- if structured output is useful for the task, prefer `EXPORTPLCBLOCKSMARTJSON`
- if the user explicitly asks for a specific format, use either:
  - `EXPORTPLCBLOCK`
  - `EXPORTPLCBLOCKDOCS`

After exporting:
- report which strategy was used
- list the generated files
- explain briefly why that format was chosen for the block type/language
