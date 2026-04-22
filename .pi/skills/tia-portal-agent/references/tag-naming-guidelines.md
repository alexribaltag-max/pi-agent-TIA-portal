# TIA Tag Naming Guidelines

These are the default naming and organization standards for PLC tags, HMI tags, DB members, and signal-related interfaces in this repository.

If a project already follows a different but consistent convention, preserve the existing convention unless the user explicitly asks to standardize or migrate names.

## When to read this file

Read this file for tasks involving:
- PLC tag creation, update, deletion, or cleanup
- HMI tag creation, update, deletion, or cleanup
- DB member naming
- interface signal naming
- tag-table organization
- naming review or naming proposals
- cross-reference analysis before renaming

## Core naming principles

1. **Names must describe function, not storage.**
2. **Names must be stable.** Do not rename referenced signals casually.
3. **One concept, one name.** The same signal should not have three different names across PLC, HMI, and diagnostics unless there is a clear integration reason.
4. **Prefer full words over cryptic abbreviations.**
5. **Do not encode temporary implementation details** such as byte offsets, tag-table order, or test status into production tag names.
6. **Make booleans read naturally.**
7. **Keep naming consistent across similar equipment.**

## Allowed character and format rules

- Use only letters, numbers, and underscore when naming PLC or HMI tags intended for broad project use.
- Do not use spaces.
- Do not use hyphens in tag names.
- Start names with a letter.
- Use **PascalCase** inside each token and underscore only as a level separator.

Recommended overall pattern:

`<Area>_<Unit>_<SignalName><SemanticSuffix>`

Examples:
- `FeedConv_MotorRunCmd`
- `FeedConv_MotorRunningSts`
- `Sealer_JawTempHighAlm`
- `Hmi_ResetReq`
- `Line01_AirPressureLowAlm`

## Standard token order

Use this order where possible:

1. **Area or machine section**
2. **Unit or equipment**
3. **Signal meaning**
4. **Semantic suffix**

Examples:
- `Infeed_Belt_StartCmd`
- `Infeed_Belt_RunningSts`
- `Outfeed_Pusher_HomeFb`
- `Utilities_AirPressureLowAlm`

Do not reverse meaning casually, such as mixing `Motor_StartConv` and `Conv_MotorStart` in the same project.

## Approved semantic suffixes

Use these standard suffixes consistently.

### Command / request / control
- `Cmd` = command output or command intent
- `Req` = request from another system, HMI, or sequence
- `En` = enable condition or feature enable
- `Perm` = permissive
- `Rst` = reset command
- `Ack` = acknowledgement request or acknowledgement result, depending on context

### Status / feedback / condition
- `Sts` = general status
- `Fb` = physical or logical feedback
- `Act` = actual value
- `Valid` = validity state
- `Ready` = ready condition
- `Busy` = operation in progress
- `Done` = operation completed
- `Ok` = positive health/quality condition

### Alarm / fault / diagnostic
- `Alm` = alarm condition
- `Wrn` = warning condition
- `Fault` = fault condition
- `Trip` = tripped state
- `Diag` = diagnostic information

### Analog/value semantics
- `Sp` = setpoint
- `Pv` = process value, only if that convention already exists
- `Raw` = raw unscaled value
- `Pct` = percentage value
- `Ms`, `S`, `Min` = time unit suffix when required and unambiguous

## Boolean naming rules

Boolean names must read like a condition, request, or command.

Good:
- `MotorRunCmd`
- `MotorRunningSts`
- `GuardDoorOpenSts`
- `ResetReq`
- `VacuumOk`
- `TempHighAlm`

Bad:
- `Motor`
- `Bit12`
- `GeneralFlag`
- `X1`
- `DoorStatusBool`

### Boolean conventions by meaning

- Operator or sequence demand: `StartReq`, `StopReq`, `ResetReq`
- PLC-issued outputs/commands: `MotorRunCmd`, `ValveOpenCmd`
- Real feedbacks: `MotorRunningFb`, `ValveOpenFb`
- Derived status: `AutoModeSts`, `CycleActiveSts`
- Alarm states: `OverloadAlm`, `PressureLowAlm`

## Analog and numeric naming rules

Analog and numeric names should indicate both meaning and, when needed, unit.

Good examples:
- `JawTempSp`
- `JawTempAct`
- `ConveyorSpeedPct`
- `CycleTimeMs`
- `AirPressureBar`

Rules:
- Include engineering unit in the name when values may otherwise be ambiguous.
- Do not mix raw and engineering values under similar names.
- Use `Raw` for hardware-near values and a unit-bearing name for scaled values.

Example:
- `TankLevelRaw`
- `TankLevelPct`

## HMI tag naming rules

- Prefer HMI tags to match their PLC source signal names whenever practical.
- If an HMI tag is a mapped PLC variable, reuse the PLC name exactly or add a minimal HMI context prefix only when needed.
- Use `Hmi_` prefix only for HMI-internal tags, not for direct PLC mirror tags.

Examples:
- PLC signal mirrored to HMI: `FeedConv_MotorRunningSts`
- HMI-only command buffer: `Hmi_SelectedRecipeNo`
- HMI popup state: `Hmi_AlarmPopupVisible`

## DB member naming rules

- Use the same semantic naming rules for DB members as for tags.
- Structure names by meaning, not by type.
- Do not prefix every member with data type abbreviations such as `b`, `i`, `r`, `dw` unless the project already depends on that legacy pattern.

Good:
- `StartReq`
- `MotorCurrentAct`
- `AlarmResetReq`
- `RecipeNumber`

Avoid:
- `bStart`
- `iRecipe`
- `rSpeed`

## Interface naming rules for FB/FC parameters

- Inputs and outputs should use the same semantic names as related tags.
- Do not add `In` or `Out` prefixes when parameter direction already provides that information.
- Use a structured UDT when many related signals belong together.

Good:
- `StartReq`
- `StopReq`
- `MotorRunningFb`
- `FaultActive`

Avoid:
- `In_StartReq`
- `Out_MotorRun`

## Approved abbreviations

Use abbreviations sparingly and consistently.

Generally acceptable:
- `Hmi`
- `Fb`
- `Db`
- `Fc`
- `Cmd`
- `Req`
- `Sts`
- `Alm`
- `Wrn`
- `Act`
- `Sp`
- `Pct`
- `Diag`

Avoid unless already established in the project:
- `Flg`
- `Tmp`
- `Misc`
- `Gen`
- `Val` for too many different meanings
- vendor- or site-specific abbreviations that new engineers will not understand

## Tag-table organization standards

Organize tags by ownership and function.

Recommended table strategy:
- one table for machine-global shared signals
- one or more tables per machine area or module
- separate utility/common-service signals where appropriate
- keep test or temporary commissioning tags out of production tables when possible

Recommended examples:
- `01_Global`
- `10_Infeed`
- `20_Sealer`
- `30_Outfeed`
- `90_Service`

If the project already uses a numbering convention, preserve it.

## Renaming policy

Before renaming an existing PLC or HMI tag:
- inspect cross references
- identify HMI bindings, alarms, scripts, and external consumers
- prefer keeping the old name if there is any uncertainty
- if a rename is needed, report the likely impact clearly

The agent must not perform broad renames without explicit user approval.

## What to avoid

Do not create names like:
- `Tag1`
- `SpareBool3`
- `TempSignal`
- `NewTag`
- `Test123`
- `M10_0_Start`

Do not mix styles like:
- `motor_run_cmd`
- `MotorRunCMD`
- `Motor_Run_Command`

in the same project unless preserving a legacy area intentionally.

## Validation checklist

Before creating or changing a tag, verify:
- name reflects machine function
- suffix reflects semantic meaning
- name is unique in the intended scope
- data type matches intended use
- address or connection matches intended signal
- table placement is correct
- HMI/PLC alignment is preserved where required
- rename impact was considered if the tag already existed
