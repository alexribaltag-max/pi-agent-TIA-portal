# Session Findings for TiaLocalBridge

## Device references
- Device names may contain `/`
- `GETDEVICES` was updated to return a usable `Reference=...`
- downstream commands should use that reference

## Device items
- `GETDEVICEITEMS` is the command used to list PLC modules/device items
- help text was updated to make this clearer

## PLC tags
- PLC tags can be created, updated, and deleted directly through the TIA API
- no export/import workflow is needed for normal PLC tag edits

## PLC blocks
- block discovery was added before content export/import
- metadata available includes type, language, group, number, consistency, protection, timestamps, and instance DB relationship

## Block export findings

### LAD
- best document export behavior observed
- document export created `.s7dcl` and `.s7res`

### SCL
- XML export works well and exposes code structure
- document export failed in tested examples with a know-how-protected error
- this happened even when `GETPLCBLOCKINFO` reported `KnowHowProtected=False`

### DB
- XML export works
- document export works and produces `.s7dcl`

### Instance DB
- XML export works
- document export works and produces `.s7dcl`

## Smart export behavior adopted
- SCL -> XML
- DB -> documents
- InstanceDB -> documents
- LAD / other non-SCL blocks -> documents
- fallback to XML if document export fails

## Smart import test result
A renamed DB block import was tested successfully.

Test summary:
- original source block: `02_Global/Data`
- imported renamed block: `02_Global/Data_ImportedTest`
- import method: `IMPORTPLCBLOCKSMART` using `.s7dcl`
- result: success

Post-import observation:
- imported block existed and was readable through `GETPLCBLOCKINFO`
- imported block showed `IsConsistent=False`
- imported block had no meaningful compile date yet

Implication:
- import works, and the bridge now includes `COMPILEPLCBLOCK`, `COMPILEPLC`, and `UPDATEPROGRAM` for the next step after import
- import responses now report `IsConsistent` and `CompileDate` for imported blocks so follow-up actions are easier to judge

## CREATEFB finding and workaround
Direct Openness `PlcBlockComposition.CreateFB(...)` was tested in this environment with multiple languages.

Observed behavior:
- `SCL`, `LAD`, `FBD`, `STL`, and `GRAPH` creation attempts failed
- `ProDiag` creation succeeded
- common failure text: `The action "Create block" only supports the programming language 'ProDiag'.`

Implication:
- the bridge should not rely on direct `CreateFB(..., ProgrammingLanguage.SCL)` in this repo environment
- `CREATEFB` was changed to use XML template import instead
- the same template-import pattern is now also used for `CREATEFC` and `CREATEDB`

## CREATEFB / COMPILEPLCBLOCK / DELETEPLCBLOCK test result
Template-based `CREATEFB` was tested successfully against `PackagingMachine/S7-1500/ET200MP station_1`.

Test summary:
- created block: `02_Global/Fb_AgentTest_<timestamp>`
- creation result: success
- created type/language: `FB` / `SCL`
- initial state after create: `IsConsistent=False`, no meaningful compile date
- `COMPILEPLCBLOCK` result: success, `Errors=0`, `Warnings=0`
- post-compile state: `IsConsistent=True`, compile date populated
- `DELETEPLCBLOCK` cleanup: success

Implication:
- template import is a practical workaround for disposable SCL FB creation
- treat created FBs like imported objects and expect compile/update follow-up when needed
- prefer these created objects for safe rename/delete regression tests

## Minimal template refinement
The original template-based block creation workflow has been refined so the built-in creation templates are cleaner and smaller.

Current state:
- `CREATEFB` uses a minimal SCL FB template
- `CREATEFC` uses a minimal SCL FC template
- `CREATEDB` uses a minimal global DB template

Implication:
- created disposable blocks are easier to inspect
- the creation workflow is more consistent across FB, FC, and DB creation

## CREATEFC status
`CREATEFC` is implemented with the same import-style template workflow as `CREATEFB`.

Current understanding:
- it creates a disposable SCL FC from a minimal built-in template
- it should be treated like an imported block for compile/update follow-up
- this reference file does not yet record a dedicated live test result for `CREATEFC`

## CREATEDB status
`CREATEDB` is implemented with a minimal global DB template import workflow.

Current understanding:
- it creates a disposable global DB in a target block group
- DB number allocation avoids collisions with existing global DBs and instance DBs
- this reference file does not yet record a dedicated live test result for `CREATEDB`
