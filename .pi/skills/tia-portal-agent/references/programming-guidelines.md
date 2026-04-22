# TIA Programming Guidelines

These are the default engineering standards for PLC programming tasks in this repository.

If an existing project already follows a different but internally consistent convention, preserve the project convention unless the user explicitly asks for a migration.

## When to read this file

Read this file for any TIA task involving:
- PLC blocks
- PLC logic generation, refactoring, or review
- block imports or exports
- DB, FB, or FC creation
- compile, update, or interface-impact decisions
- code review or architecture review

This is the **default context** for nearly all TIA engineering work.

## Primary engineering principles

1. **Discover before mutating.** Inspect devices, groups, blocks, tags, or hardware before making changes.
2. **Make the smallest safe change.** Do not refactor adjacent logic unless it is required.
3. **Preserve working interfaces.** Do not change parameter names, types, directions, or DB layout unless explicitly requested.
4. **Prefer clarity over cleverness.** Generated logic must be readable by a maintenance engineer at 2 AM.
5. **Be deterministic.** Logic must behave predictably scan-to-scan.
6. **Keep responsibility local.** One block should have one clear role.
7. **Avoid hidden side effects.** Inputs are read, outputs are assigned clearly, and internal state is explicit.
8. **Do not silently widen scope.** If a requested change affects alarms, HMI, safety, hardware, or interfaces, state it.
9. **After structural changes, recommend compile/update.** Especially after imports, interface changes, and hardware-linked logic changes.
10. **Never assume safety behavior.** Safety-related behavior must not be invented or changed without explicit instruction.

## Block design standards

## 1. Choose the right block type

- Use **FB** for reusable logic that requires instance memory.
- Use **FC** for stateless calculations or logic without retained instance state.
- Use **DB** for structured data, recipe/configuration storage, or instance data.
- Do not put unrelated machine behavior into a global DB when it belongs in an FB interface or static area.

## 2. Keep blocks focused

A block should represent one of these:
- a machine module
- a device abstraction
- a sequence/state machine
- a calculation/utility function
- a data container

Avoid blocks that mix:
- IO mapping
- sequencing
- alarm generation
- HMI formatting
- diagnostics

unless the existing project convention already couples them and the user asked for a local modification only.

## 3. Preferred block structure

When writing or refactoring SCL logic, keep this order where practical:

1. short block purpose comment
2. interface declarations
3. internal constants/types if used
4. input normalization / edge detection
5. permissives / interlocks
6. main state logic
7. output assignment
8. diagnostics / status flags

## 4. Interface rules

- Interfaces must be explicit and stable.
- Input parameters should describe what the block needs, not how the caller stores it.
- Output parameters should describe results or commands, not internal implementation details.
- Use `InOut` only when true bidirectional structured access is required.
- Avoid oversized interfaces; prefer a small structured UDT or a clean set of parameters.
- If changing a public interface is unavoidable, explicitly warn the user about downstream call-site impact.

## Naming standards for blocks and members

## Block names

Use clear PascalCase names without spaces.

Recommended patterns:
- FB: `FbMotorControl`, `FbBagClamp`, `FbAxisHoming`
- FC: `FcScaleRawToEng`, `FcLimitReal`, `FcCalcDewPoint`
- Global DB: `DbMachineConfig`, `DbAlarmConfig`, `DbRecipeActive`
- Instance DB: default TIA-generated instance style is acceptable; do not rename large sets of instance DBs unless requested.

Rules:
- Prefix block type in the name: `Fb`, `Fc`, `Db`.
- After the prefix, use a function-oriented name.
- Do not encode version numbers in block names.
- Do not use vague names like `Test`, `TempLogic`, `NewBlock`, `Utilities2` in real project logic.

## Parameter/member names

- Use PascalCase for FB/FC interface members and DB members.
- Boolean names should read as a condition or command, such as `StartReq`, `MotorRunning`, `AlarmActive`.
- Avoid single-letter names except loop indices in very small local scopes.
- Avoid ambiguous abbreviations unless they are already established in the project.

## Language standards

## Preferred language choices

- Prefer **SCL** for new algorithmic logic, calculations, data handling, and complex sequencing.
- Preserve **LAD/FBD** when modifying existing maintenance-facing logic unless the user requests migration.
- Do not migrate block language just because another style is preferred.

## SCL style rules

- Write simple, explicit assignments.
- Prefer `IF`, `CASE`, and small helper calculations over deeply nested expressions.
- Keep nesting shallow where possible.
- Use intermediate variables when an expression is harder to read than two or three clear assignments.
- Avoid repeated evaluation of the same complex condition.
- Use `CASE` for mutually exclusive states or modes.
- Keep scan-dependent behavior obvious.

### Example style preference

Prefer:

```scl
MotorPermitted := Enable AND NOT FaultActive AND GuardClosed;

IF MotorPermitted AND StartReq THEN
    MotorCmd := TRUE;
ELSIF StopReq OR NOT MotorPermitted THEN
    MotorCmd := FALSE;
END_IF;
```

Over compressed logic that hides intent.

## State machine standards

When implementing sequences:
- Use an explicit state variable.
- Use named state constants or a typed enum if the project pattern allows it.
- Separate transition conditions from output behavior.
- Provide a defined idle/reset state.
- Provide timeout/error handling for states that wait on external feedback.
- Do not scatter sequence transitions across unrelated code regions.

Recommended sequence outputs:
- current state
- busy/active
- done/complete
- error/fault
- error code or status code when relevant

## Alarm and diagnostics standards

- Separate command logic from alarm logic when practical.
- Alarm bits should represent clear machine conditions.
- Do not create duplicate alarms for the same root cause unless there is a user-facing reason.
- Prefer latched alarm handling only when the project convention requires operator acknowledgement.
- Expose diagnostic status in a way that HMI and service logic can consume consistently.
- If adding a new alarm, note likely HMI/tag impacts.

## Data and DB standards

- Use structured DBs instead of flat unrelated members when data belongs together.
- Keep configuration, runtime state, and diagnostics logically separated.
- Do not reorder existing DB members unless necessary; it may affect external dependencies.
- Retain values only when the process truly needs retention.
- Avoid using global DBs as uncontrolled shared memory.

## Instance and multi-instance guidance

- Use instance DBs for FB state.
- Prefer multi-instance organization when it improves modularity and matches the current project style.
- Do not convert between instance strategies unless the user requests architectural change.

## Commenting standards

- Comments must explain **why**, not restate obvious code.
- Add a short block header comment for generated logic.
- Comment only non-obvious calculations, state transitions, or safety assumptions.
- Remove placeholder comments like `// TODO`, `// temp`, or `// test` from final delivered code unless they are truly required.

Recommended block header:
- purpose
- important assumptions
- external dependencies if any

## Change management rules for the agent

When the agent performs TIA work:
- Identify the exact target device reference.
- Identify the exact target block group or block reference.
- Discover existing blocks before creating or renaming blocks.
- Prefer creating disposable test blocks for experiments.
- Warn before overwrite, rename, delete, or import actions.
- After import, compile or recommend compile/update as appropriate.
- If a block import succeeds, explicitly note that compile/update may still be required in TIA Portal.

## What to avoid

Do not:
- invent safety logic without specification
- change address-dependent behavior without checking tags/hardware impact
- merge unrelated responsibilities into one block
- create generic catch-all utility blocks without a clear use case
- hide state in scattered markers when a proper FB or DB structure is more appropriate
- rename large interfaces or block families without user approval

## Review checklist

Before finishing a TIA programming task, verify:
- correct project and device reference used
- correct block group and target block used
- block type fits the requested behavior
- naming follows the conventions above or existing project convention
- interfaces were preserved unless change was explicitly requested
- logic is deterministic and readable
- alarm/diagnostic impact was considered
- compile/update recommendation was stated when relevant
