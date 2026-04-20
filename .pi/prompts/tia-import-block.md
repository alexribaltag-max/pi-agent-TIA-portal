---
description: Import a PLC block from XML or .s7dcl using smart import.
---
Use the `tia-portal-agent` skill.

Import a PLC block with these inputs:
- device reference: $1
- source path: $2
- target group reference: $3
- extra user instructions: ${@:4}

Workflow:
1. if needed, inspect available groups first with `GETPLCBLOCKGROUPS|$1`
2. import with `IMPORTPLCBLOCKSMART|$1|$2|$3`, or `IMPORTPLCBLOCKSMARTJSON|$1|$2|$3` when structured output is better for the task
3. inspect the imported block if the new name can be determined
4. warn if the imported block appears to require compile/update afterward

Be explicit about any project modifications being made.
