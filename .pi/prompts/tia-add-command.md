---
description: Add or modify a TiaLocalBridge command end-to-end, including registration, docs, and verification.
---
Use the `tia-portal-agent` skill.

Task:
$@

Required implementation checklist:
1. read `TiaLocalBridge/README.md`
2. inspect the closest existing command implementation in `TiaLocalBridge/Commands/`
3. update shared helpers in `CommandSupport.cs` only if needed
4. register the command in `Program.cs`
5. include the file in `TiaLocalBridge.csproj`
6. rebuild the project
7. verify `HELP`
8. update `README.md` with the new behavior

When done, summarize:
- files changed
- build result
- verification result
- example usage
