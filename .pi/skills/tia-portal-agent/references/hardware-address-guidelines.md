# TIA Hardware and Address Guidelines

These are the default standards for hardware discovery, module placement, IO addressing, network setup, and drive telegram addressing in this repository.

If a project already follows a different validated hardware convention, preserve the existing convention unless the user explicitly asks to reorganize it.

## When to read this file

Read this file for tasks involving:
- devices and device items
- rack and module configuration
- plug locations and slot assignment
- hardware properties
- IO start addresses
- network interfaces, IP settings, subnets, and PROFINET connectivity
- drive telegram numbers and telegram addresses

## Core engineering rules

1. **Discover before changing.** Always inspect the live project state first.
2. **Use the exact device reference returned by `GETDEVICES`.**
3. **Confirm the exact target item reference before setting hardware properties or addresses.**
4. **Make one hardware change at a time and verify after each change.**
5. **Avoid address moves unless clearly necessary.** Address changes ripple into PLC logic, HMI, diagnostics, drives, and commissioning documents.
6. **Prefer stable, contiguous allocation.**
7. **Do not create overlaps or hidden gaps without stating why.**
8. **If risk exists, say so explicitly before mutating.**

## Mandatory workflow for hardware changes

For hardware or addressing work, follow this order:

1. `GETDEVICES`
2. `GETDEVICEITEMS`
3. one or more of:
   - `GETPLUGLOCATIONS`
   - `GETHWPROPERTIES`
   - `GETHWADDRESSES`
   - `GETNETWORKINTERFACES`
   - `GETNODEPROPERTIES`
   - `GETDRIVEOBJECTS`
   - `GETDRIVETELEGRAMS`
4. perform the smallest required mutation
5. re-read the affected configuration to confirm the result

The agent should report both the command used to inspect and the command used to mutate.

## Device and module structure standards

- Keep device naming stable once hardware is referenced downstream.
- Reuse existing rack/station naming style when adding equipment.
- Insert modules only in verified valid plug locations.
- Do not assume slot numbering; inspect valid locations first.
- When adding modules to distributed IO, preserve left-to-right logical grouping where the project already uses it.

Preferred grouping order where a new station is being built and no project standard exists:
1. head/interface module
2. digital inputs
3. digital outputs
4. analog inputs
5. analog outputs
6. technology/special modules
7. communication/safety-specific modules as required by design

## IO addressing standards

## General rules
- For ip adresses we use 10.178.0.x where PLC's start at 10.178.0.2 to 10, we reserve form 70 to 90 for external TIA devices , drives start at 100 to 200 other devices from 200 to 250
- IO adresses : main PLC starts at 0 only digital inouts except for safety DI DO wich start at 50  , analog , counters or technoological objects form 500 to 999, drives telegrams start at 1000 to 2000 , safety telegram drives start at 2000 .
- Keep addresses contiguous within a station where practical.
- Prefer alignment that makes maintenance easier.
- Avoid reusing old free spaces inside a station if it creates fragmented addressing unless the project already relies on dense packing.
- Keep input and output regions clearly documented and separated.
- Verify the exact child item reference that holds the address before editing.

## Preferred address allocation behavior

When a new address must be assigned:
- preserve the station's current addressing pattern
- allocate after the highest used address in that logical area when feasible
- keep related modules grouped in ascending order
- avoid shifting existing modules to make cosmetic improvements unless explicitly requested

## Alignment guidance

Use alignment as a preference, not a blind rule:
- digital modules: prefer byte-aligned starts
- analog/special modules: respect the module's natural size and current project pattern
- telegram areas: keep full telegram blocks contiguous

## Change policy

- Never change an address solely to make numbering look nicer.
- Before changing an existing start address, state that external references may break.
- After setting an address, re-read with `GETHWADDRESSES` or the relevant telegram read command.

## Network and interface standards

- Inspect interface names first; do not guess them.
- Reuse the existing subnet name if the device belongs on an existing network.
- Keep PROFINET naming consistent with the project's current convention.
- When setting IP-related properties, verify the interface and property name before writing.
- Do not connect devices across networks casually; state assumptions when creating or linking subnets.

### Preferred network naming style

If a new subnet or interface-related object must be named and no project standard exists, prefer clear functional names such as:
- `PN_Main`
- `PN_Line01`
- `PN_Packaging`
- `Service_Network`

Avoid names like:
- `Subnet1`
- `NewPN`
- `TestNet`

## PROFINET connection rules

Before using `CONNECTPROFINET`:
- confirm both device references
- confirm both interface names exactly
- confirm the intended subnet name
- state master/slave roles clearly

After connection changes:
- re-read network interfaces or node properties if verification is required
- mention any unresolved naming or topology ambiguity

## Drive telegram standards

For drives, always inspect before mutating:
1. `GETDEVICEITEMS`
2. `GETDRIVEOBJECTS`
3. `GETDRIVETELEGRAMS`

### Telegram rules

- Do not change telegram number and IO addresses in one blind step without reading current values first.
- Keep input and output telegram addresses coordinated.
- Do not create overlaps with standard IO or other telegram areas.
- If the drive has multiple drive objects, confirm which object number is being changed.

### Preferred drive addressing behavior

- Keep main telegrams in contiguous ranges.
- Keep safety telegrams clearly separated or consistently paired according to the project's existing pattern.
- Report both old and new telegram settings when changed.

## Hardware property change rules

Before using `SETHWPROPERTY`, `SETNODEPROPERTY`, or `SETHWADDRESS`:
- confirm the exact target reference
- confirm the exact property name if applicable
- describe the intended result briefly
- avoid batch property edits unless the user explicitly asks for them

## Risk reporting rules

When a hardware change may affect other engineering areas, say so explicitly. Typical downstream impacts include:
- PLC tag address assumptions
- HMI communication mappings
- drive communication
- diagnostics/alarm texts
- commissioning documents
- external systems or fieldbus mappings

## What to avoid

Do not:
- guess plug positions
- guess interface names
- change several station addresses at once without verification in between
- reorganize a whole rack to make it look cleaner unless requested
- move existing modules if the task only requires adding a new module
- change network settings without stating the likely impact
- assign overlapping IO or telegram ranges

## Validation checklist

Before finishing a hardware-related task, verify:
- correct project and device reference used
- correct module or item reference used
- correct interface name used where applicable
- current state was inspected before mutation
- no overlap was introduced
- updated state was re-read after mutation
- user was informed of downstream impact when relevant
