---
description: Explore PLC block groups, blocks, and metadata before export or import.
---
Use the `tia-portal-agent` skill.

Investigate PLC blocks for:
- device reference: $1
- user context: ${@:2}

Required workflow:
1. `GETPLCBLOCKGROUPS|$1`
2. `GETPLCBLOCKS|$1` or `GETPLCBLOCKSJSON|$1` when structured output is better for the task
3. if a specific block is relevant, use `GETPLCBLOCKINFO|$1|<block-reference>` or `GETPLCBLOCKINFOJSON|$1|<block-reference>` when structured metadata is better for the task

Focus on:
- block type
- programming language
- group reference
- know-how protection behavior
- whether the next best step should be export, import, compile/update, create a disposable test block, delete a disposable test block, or rename a disposable test block

Return a concise recommendation for the next action.
