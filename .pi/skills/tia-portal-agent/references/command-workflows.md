# TiaLocalBridge Command Workflows

## Device discovery workflow

Use this when the user wants PLC modules, HMI devices, or a valid device reference.

1. List projects:
   - `LIST`
2. List devices:
   - `GETDEVICES|<project-name>`
3. Use the returned `Reference=...` value for downstream commands.
4. If the user asks for PLC modules, use:
   - `GETDEVICEITEMS|<device-reference>`

### Example
```text
LIST
GETDEVICES|PackagingMachine
GETDEVICEITEMS|PackagingMachine/S7-1500/ET200MP station_1
```

## PLC tag workflow

1. Discover device reference with `GETDEVICES`
2. List tag tables:
   - `GETPLCTAGTABLES|<device-reference>`
3. List tags:
   - `GETPLCTAGS|<device-reference>`
4. Modify tags directly:
   - `ADDPLCTAG`
   - `UPDATEPLCTAG`
   - `DELETEPLCTAG`

### Example
```text
GETPLCTAGTABLES|PackagingMachine/S7-1500/ET200MP station_1
GETPLCTAGS|PackagingMachine/S7-1500/ET200MP station_1
ADDPLCTAG|PackagingMachine/S7-1500/ET200MP station_1|02_Global/Global|MyTag|Bool|%M20.0
```

## PLC block exploration workflow

Always inspect before exporting or importing.

1. List groups:
   - `GETPLCBLOCKGROUPS|<device-reference>`
2. List blocks:
   - `GETPLCBLOCKS|<device-reference>`
3. Inspect one block:
   - `GETPLCBLOCKINFO|<device-reference>|<block-reference>`

### Example
```text
GETPLCBLOCKGROUPS|PackagingMachine/S7-1500/ET200MP station_1
GETPLCBLOCKS|PackagingMachine/S7-1500/ET200MP station_1
GETPLCBLOCKINFO|PackagingMachine/S7-1500/ET200MP station_1|02_Global/Data
```

## PLC block export workflow

### Safe general choice
Use:
```text
EXPORTPLCBLOCKSMART|<device-reference>|<block-reference>|<target-directory>
```

### When specific format is needed
Use XML export:
```text
EXPORTPLCBLOCK|<device-reference>|<block-reference>|<target-file-path>
```

Use document export:
```text
EXPORTPLCBLOCKDOCS|<device-reference>|<block-reference>|<target-directory>|[file-name-without-extension]
```

## PLC block import workflow

Use smart import when the source is either XML or `.s7dcl`.

```text
IMPORTPLCBLOCKSMART|<device-reference>|<source-path>|[target-group-reference]
```

Supported inputs:
- `.xml`
- `.s7dcl`
- folder containing exactly one `.s7dcl`

### Example
```text
IMPORTPLCBLOCKSMART|PackagingMachine/S7-1500/ET200MP station_1|C:\Exports\Data_docs\Data.s7dcl|02_Global
```

## Disposable block creation workflow

In this repo environment, direct Openness `CreateFB(...)` for normal languages was observed to fail with a ProDiag-only limitation.
Because of that, block creation in this bridge uses internal XML template import workflows.

Currently available creation commands:
- `CREATEFB` -> minimal SCL FB template
- `CREATEFC` -> minimal SCL FC template
- `CREATEDB` -> minimal global DB template

### Recommended sequence
1. Create the disposable block:
   - `CREATEFB|<device-reference>|<target-group-reference>|<block-name>`
   - `CREATEFC|<device-reference>|<target-group-reference>|<block-name>`
   - `CREATEDB|<device-reference>|<target-group-reference>|<block-name>`
2. Inspect it:
   - `GETPLCBLOCKINFO|<device-reference>|<target-group-reference>/<block-name>`
3. Compile it if needed:
   - `COMPILEPLCBLOCK|<device-reference>|<target-group-reference>/<block-name>`
4. Delete it after the test:
   - `DELETEPLCBLOCK|<device-reference>|<target-group-reference>/<block-name>`

### FB example
```text
CREATEFB|PackagingMachine/S7-1500/ET200MP station_1|02_Global|Fb_AgentTest
GETPLCBLOCKINFO|PackagingMachine/S7-1500/ET200MP station_1|02_Global/Fb_AgentTest
COMPILEPLCBLOCK|PackagingMachine/S7-1500/ET200MP station_1|02_Global/Fb_AgentTest
DELETEPLCBLOCK|PackagingMachine/S7-1500/ET200MP station_1|02_Global/Fb_AgentTest
```

### FC example
```text
CREATEFC|PackagingMachine/S7-1500/ET200MP station_1|02_Global|Fc_AgentTest
GETPLCBLOCKINFO|PackagingMachine/S7-1500/ET200MP station_1|02_Global/Fc_AgentTest
COMPILEPLCBLOCK|PackagingMachine/S7-1500/ET200MP station_1|02_Global/Fc_AgentTest
DELETEPLCBLOCK|PackagingMachine/S7-1500/ET200MP station_1|02_Global/Fc_AgentTest
```

### DB example
```text
CREATEDB|PackagingMachine/S7-1500/ET200MP station_1|02_Global|Db_AgentTest
GETPLCBLOCKINFO|PackagingMachine/S7-1500/ET200MP station_1|02_Global/Db_AgentTest
COMPILEPLCBLOCK|PackagingMachine/S7-1500/ET200MP station_1|02_Global/Db_AgentTest
DELETEPLCBLOCK|PackagingMachine/S7-1500/ET200MP station_1|02_Global/Db_AgentTest
```

Notes:
- treat these created blocks like imported objects
- compile/update follow-up may still be required after creation
- prefer these disposable blocks for rename/delete tests instead of existing project blocks
- `CREATEDB` is implemented and documented, but live project validation should still be preferred before relying on it for larger workflows

## Post-import guidance

After import, inspect the new block with:
```text
GETPLCBLOCKINFO|<device-reference>|<block-reference>
```

If the block shows:
- `IsConsistent=False`
- empty/default compile date

then use one or more of these follow-up commands:
```text
COMPILEPLCBLOCK|<device-reference>|<block-reference>
COMPILEPLC|<device-reference>
UPDATEPROGRAM|<device-reference>
```

`COMPILEPLCBLOCK` is the narrowest option.
`COMPILEPLC` gives a broader PLC software compile result.
`UPDATEPROGRAM` refreshes the PLC program at the software level after imports or changes.
