---
description: Explore or modify PLC tags through TiaLocalBridge.
---
Use the `tia-portal-agent` skill.

Work on PLC tags for this target:
- device reference: $1
- user request details: ${@:2}

Workflow:
1. confirm or discover the PLC tag tables with `GETPLCTAGTABLES|$1`
2. inspect tags with `GETPLCTAGS|$1`
3. if the user wants changes, use the direct PLC tag commands:
   - `ADDPLCTAG`
   - `UPDATEPLCTAG`
   - `DELETEPLCTAG`
4. summarize the exact commands and resulting tag state

Be careful to target the correct table reference.
