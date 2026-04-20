---
description: Discover TIA project devices, PLC modules, and usable device references.
---
Use the `tia-portal-agent` skill and inspect the TiaLocalBridge project workflow for discovery.

Goal:
- discover the relevant TIA project, devices, and PLC modules
- use `GETDEVICES` first
- use the returned device `Reference=...` values for downstream commands
- if the device is a PLC, use `GETDEVICEITEMS` to list PLC modules

User context:
$@

Deliver:
- the exact commands run
- the important results
- the correct device references to reuse next
- any ambiguity or follow-up commands needed
