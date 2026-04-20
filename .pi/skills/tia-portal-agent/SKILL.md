---
name: tia-portal-agent
description: TIA Portal automation workflow using the TiaLocalBridge extension. Use when the user asks about TIA Portal devices, PLC modules, PLC tags, HMI tags, PLC blocks, block export/import, or to perform TIA tasks.
---

# TIA Portal Agent

Use this skill when you need to interact with TIA Portal using the `tiabridge` tool.

## What this skill is for

This skill helps you act as a **TIA Portal Coding Agent**. You automate tasks in TIA Portal by sending commands to the `tiabridge` process (which runs `TiaLocalBridge.exe` in the background).

Use it when the user wants to:
- open or inspect TIA Portal projects
- list devices, PLC modules, PLC tags, HMI tags, or PLC blocks
- add, update, or delete PLC tags
- create, export, import, or compile PLC blocks
- understand automation capabilities using the bridge

## Your Role

You are **NOT** here to develop the bridge itself unless explicitly asked. Your main job is to **USE** the bridge to automate the user's TIA Portal projects.
When you need to interact with TIA Portal, use the `tiabridge` custom tool. It expects a single string `command` in the format `COMMAND|arg1|arg2`. 

## Main References
- `../../../TiaLocalBridge/README.md` (Read this to understand what commands the bridge supports and their exact syntax).
- `../../../TiaLocalBridge/scripts/` (Check this folder for example PowerShell workflows, bridge command sequences, research probes, and test patterns that show how the bridge and Openness APIs were exercised during development).

## Working Rules

1. **Use the `tiabridge` tool** instead of directly interacting with TIA Portal files.
2. **Read the project README first** if you are unsure of the command syntax.
3. **Check `TiaLocalBridge/scripts/` for examples** when the task resembles an existing workflow such as tag import, readonly discovery, block export/import testing, hardware setup, or process/Openness diagnostics.
4. **Prefer the example project `PackagingMachine`** for tests unless instructed otherwise.
5. **Warn that imported blocks may require compile/update afterward.**
6. **Use explicit device references from `GETDEVICES`** when working with device-specific commands.

## Current bridge capabilities

Always check `TiaLocalBridge/README.md` for the full, up-to-date list.

### Devices and modules
- `GETDEVICES|[project-name]`
- `GETDEVICESJSON|<device-reference>`
- `GETDEVICEITEMS|<device-reference>`

### Drive telegrams
- `GETDRIVEOBJECTS|<device-reference>`
- `GETDRIVETELEGRAMS|<device-reference>|<device-item-reference>|[drive-object-number]`
- `SETDRIVETELEGRAMNUMBER|<device-reference>|<device-item-reference>|<telegram-type>|<telegram-number>|[drive-object-number]`
- `SETDRIVETELEGRAMADDRESS|<device-reference>|<device-item-reference>|<telegram-type>|<io-type>|<start-address>|[drive-object-number]`

### Network and Subnet (PROFINET)
- `CREATESUBNET|<project-name>|<subnet-name>`
- `GETNETWORKINTERFACES|<device-reference>`
- `CONNECTTOSUBNET|<device-reference>|<interface-name>|<subnet-name>`
- `CONNECTPROFINET|<master-device-ref>|<master-interface-name>|<slave-device-ref>|<slave-interface-name>|<subnet-name>`

### PLC tags
- `GETPLCTAGTABLES|<device-reference>`
- `GETPLCTAGS|<device-reference>`
- `ADDPLCTAG|<device-reference>|<table-reference>|<tag-name>|<data-type>|<logical-address>`
- `UPDATEPLCTAG|<device-reference>|<table-reference>|<tag-name>|<data-type>|<logical-address>`
- `DELETEPLCTAG|<device-reference>|<table-reference>|<tag-name>`

### PLC blocks
- `GETPLCBLOCKGROUPS|<device-reference>`
- `GETPLCBLOCKS|<device-reference>`
- `GETPLCBLOCKINFO|<device-reference>|<block-reference>`
- `CREATEFB|<device-reference>|<target-group-reference>|<block-name>`
- `CREATEFC|<device-reference>|<target-group-reference>|<block-name>`
- `CREATEDB|<device-reference>|<target-group-reference>|<block-name>`
- `DELETEPLCBLOCK|<device-reference>|<block-reference>`
- `RENAMEPLCBLOCK|<device-reference>|<block-reference>|<new-block-name>`
- `COMPILEPLCBLOCK|<device-reference>|<block-reference>`
- `COMPILEPLC|<device-reference>`
- `EXPORTPLCBLOCKSMART|<device-reference>|<block-reference>|<target-directory>`
- `IMPORTPLCBLOCKSMART|<device-reference>|<source-path>|[target-group-reference]`

## Known behavior you should remember

### Device references
Some TIA device names contain `/`. Because of that, `GETDEVICES` returns a reusable reference like:
`S7-1500/ET200MP station_1 [Reference=PackagingMachine/S7-1500/ET200MP station_1, Type=System:Device.S71500]`
Use the **Reference** value (e.g., `PackagingMachine/S7-1500/ET200MP station_1`) for all downstream commands.

### PLC blocks strategy
Always discover first:
1. `GETPLCBLOCKGROUPS`
2. `GETPLCBLOCKS`
3. `GETPLCBLOCKINFO`
Then perform actions.

## Recommended workflows

### Discover devices and PLC modules
1. Call tool `tiabridge` with `GETDEVICES|PackagingMachine`
2. Call tool `tiabridge` with `GETDEVICEITEMS|PackagingMachine/S7-1500/ET200MP station_1`

### Work with PLC tags
1. `GETPLCTAGTABLES|PackagingMachine/S7-1500/ET200MP station_1`
2. `GETPLCTAGS|PackagingMachine/S7-1500/ET200MP station_1`
3. `ADDPLCTAG|PackagingMachine/S7-1500/ET200MP station_1|02_Global/Global|MyTag|Bool|%M10.0`

### Work with drive telegrams
1. `GETDEVICEITEMS|dano/Drive_U50_1`
2. `GETDRIVEOBJECTS|dano/Drive_U50_1`
3. `GETDRIVETELEGRAMS|dano/Drive_U50_1|C18-VASP1|1`
4. `SETDRIVETELEGRAMNUMBER|dano/Drive_U50_1|C18-VASP1|MainTelegram|352|1`
5. `SETDRIVETELEGRAMNUMBER|dano/Drive_U50_1|C18-VASP1|SafetyTelegram|30|1`
6. `SETDRIVETELEGRAMADDRESS|dano/Drive_U50_1|C18-VASP1|MainTelegram|Input|1000|1`
7. `SETDRIVETELEGRAMADDRESS|dano/Drive_U50_1|C18-VASP1|MainTelegram|Output|1000|1`

### Import and Compile
1. `IMPORTPLCBLOCKSMART|PackagingMachine/S7-1500/ET200MP station_1|C:\Exports\Data_docs\Data.s7dcl|02_Global`
2. `COMPILEPLCBLOCK|PackagingMachine/S7-1500/ET200MP station_1|02_Global/Data_ImportedTest`
