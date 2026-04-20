---
description: Debug a TiaLocalBridge command by tracing command behavior, source, and live test output.
---
Use the `tia-portal-agent` skill.

Debug this issue:
$@

Approach:
1. inspect the relevant command source files
2. inspect shared helpers in `CommandSupport.cs` if resolution or formatting may be involved
3. rebuild after changes
4. run a focused live test against the example project when safe
5. verify `HELP` if descriptions/usages changed
6. explain root cause and fix clearly

Prefer the smallest safe repro possible.
