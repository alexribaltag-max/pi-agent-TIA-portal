# TIA Portal V21 Bridge Regression Checklist

Use this file as the handoff/state record between agent sessions. Update checkboxes and append actual results as tests run. Keep testing staged: start read-only, then do writes only in the disposable smoke project or disposable objects, and record cleanup.

## Goal

Exercise every registered TiaLocalBridge command/function against the V21 installation, repair confirmed defects conservatively, and rerun affected tests. Do not touch production/example project content during mutation tests.

## Environment and current test fixture

- TIA Portal: V21 (bridge connection reports `portalConnected: true`; repo README says bridge resolves V21 modular PublicAPI assemblies).
- Disposable project: `Tia21BridgeSmoke_20260617`
- Project file: `C:\Users\masias\Documents\Automation\Tia21BridgeSmoke_20260617\Tia21BridgeSmoke_20260617\Tia21BridgeSmoke_20260617.ap21`
- PLC device ref: `Tia21BridgeSmoke_20260617/PLC_1`
- CPU: `OrderNumber:6ES7 510-1DJ01-0AB0/V2.9` (CPU 1510SP-1 PN)
- PLC item ref: `0/1`; interface: `Interfaz PROFINET_1`; PLC block: `Main` (OB1/LAD).
- Smoke project status: closed in TIA before the drive stage; it is not active. Its last tested state contains CPU, DI modules in slots 2 and 4, DQ in slot 3, `ET200SP_Slave`, and a consistent Main OB. Interfaces remain connected to `PN_Smoke_20260617`; master IP was restored to `192.168.0.1`. Disposable PLC tag and FC/FB/DB tests were deleted; DI Comment remains `V21 bridge smoke test`, and DI Input was restored to 0.
- Active project: drive-test clone `20260720_V21` at `C:\Users\masias\Documents\Automation\Tia21DriveRegressionClone\20260720_V21.ap21`, copied from `C:\Users\masias\Desktop\Desarxivats\20260720_V21`. The source project was left untouched. The clone contains 11 SINAMICS drive devices and `HMI_CC20`; drive and Unified HMI regression were run only on this clone. HMI test artifacts are listed in the 2026-07-20 work log.
- Repo was already dirty before this test (V21 migration files modified). Regression fixes/docs now also cover PLC block-template culture, hardware references, drive-object getter fallback, HMI localized table/connection resolution, internal HMI tag optional fields, multilingual text old-value reporting, README guidance, and this checklist. See the work log and `git status` for the full file list.

## Status key

- `[x]` passed / completed
- `[!]` defect, unexpected result, or environment limitation; details required
- `[-]` not applicable / unavailable, with reason required
- `[ ]` not yet tested

## Stage 0 — Connection, environment, and fixture

- [x] `LIST` — bridge connected; no project before smoke test, then the new project listed.
- [x] `HELP` — command registry/usage responds.
- [x] `CREATE` — created the disposable V21 project at the path above.
- [x] `OPEN` — after the user closed the smoke project, opened the saved drive clone `.ap21` successfully. An earlier attempt while the smoke project was open correctly failed with TIA's “Another project is already open” error.
- [x] `SEARCHHWCATALOG` — found CPU 1510SP-1 PN hardware catalog entries.
- [x] `ADDDEVICE` — added the CPU to the smoke project.
- [x] `GETDEVICES` — returned reusable device reference.
- [x] `GETDEVICEITEMS` — returned rack, CPU, interface, ports, and device items.
- [x] Bridge and add-in builds passed. Commands: `dotnet msbuild TiaLocalBridge/TiaLocalBridge.csproj -target:Build -property:Configuration=Debug`; `dotnet restore TiaPiAddin/TiaPiAddin.csproj`; `dotnet msbuild TiaPiAddin/TiaPiAddin.csproj -target:Build -property:Configuration=Debug`.
- [x] After the culture fix below, bridge rebuilt successfully; restarting the bridge with `EXIT` was required because its executable was locked by the live process.

## Stage 1 — Read-only PLC, hardware, and network discovery

Fixture: `Tia21BridgeSmoke_20260617/PLC_1`.

- [x] `GETDEVICESJSON` — JSON response valid; PLC software present, no HMI software.
- [x] `GETPLCTAGTABLES` — succeeds; empty standard tag table (localized Spanish name).
- [x] `GETPLCTAGS` — succeeds; no tags, expected for new project.
- [x] `GETPLCBLOCKGROUPS` — succeeds but reports no user block groups (fresh project has root Main only).
- [x] `GETPLCBLOCKS` — Main OB1 found.
- [x] `GETPLCBLOCKSJSON` — valid JSON, one consistent Main OB1.
- [x] `GETPLCBLOCKINFO` — succeeded for Main after compile.
- [x] `GETPLCBLOCKINFOJSON` — valid JSON for Main.
- [x] `GETPLCBLOCKXREF|...|Main|AllObjects` — succeeds, zero references.
- [x] `COMPILEPLCBLOCK|...|Main` — Success, 0 errors, 0 warnings; Main became consistent.
- [x] `GETNETWORKINTERFACES` — reports `Interfaz PROFINET_1`.
- [x] `GETIPADDRESS` — reports `192.168.0.1/255.255.255.0`.
- [x] `GETNODEPROPERTIES` — returns node properties and access flags.
- [x] `GETHWPROPERTIES|...|DEVICE` — returns root-device properties and access flags.
- [x] `GETPLUGLOCATIONS|...|DEVICE` — initial result falsely reported occupied rack slots as free. Fixed to resolve a single root rack DeviceItem for rack-based stations; rerun reports available slots starting at 4, excluding occupied 1–3.
- [x] `GETHWADDRESSES|...|0/1` — no addresses for CPU item; likely expected.
- [x] `GETHWPROPERTIES|...|0/1` and `...|0/2` — succeed for CPU and inserted DI; module attributes/access modes returned.
- [x] `GETHWADDRESSES|...|0/2/1`, `...|0/3/1` — DI Input 0/8 and DQ Output 0/8 exposed.
- [x] `GETDRIVEOBJECTS` on non-drive PLC — clean `No drive objects were found` result.
- [x] PLC tag cross-reference tested after creating a disposable tag in Stage 2; see work log.
- [x] `GETPLCBLOCKSJSON`, `GETPLCBLOCKINFOJSON`, and block `AllObjects` cross-reference passed on the fresh smoke PLC; all block XREF filters were subsequently exercised on the cloned fixture (Stage 2).
- [x] JSON/text result/error protocol checks: successful text response, unknown command, missing/extra args, malformed device ref, and malformed device-item ref all returned correctly framed success/error envelopes. `GETDEVICESJSON` error also returned a valid error envelope for a missing device ref.

## Stage 2 — PLC tags and block lifecycle (isolated mutations)

Run only on the disposable smoke project or the copied drive-test clone, never the source project. Use uniquely named `AgentSmoke...` objects and marker memory (not physical IO); capture before/after state and clean up test objects when possible.

### PLC tags
- [x] `ADDPLCTAG` -> `GETPLCTAGS` -> `GETPLCTAGXREF` -> `UPDATEPLCTAG` -> verify -> `DELETEPLCTAG`; all succeeded for `AgentSmoke_Bool` at marker `%M10.0` updated to `%M10.1`, then deleted.
- [x] On cloned test PLC `20260720_V21/ET 200SP station_1`, created Bool `%M300.0`, Byte `%MB301`, Word `%MW302`, DInt `%MD304`, Real `%MD308`; updated the Byte tag to Word `%MW312`; `GETPLCTAGS` verified types/addresses. Deleted all five temporary tags and confirmed the standard table count returned to 87. Marker area only; no physical IO writes.

### PLC blocks
- [x] `CREATEFC`, inspect/info, compile (0 errors/0 warnings), delete; passed after culture fix.
- [x] `CREATEFB`, compile (0 errors/0 warnings), delete; passed after culture fix.
- [x] `CREATEDB`, compile (0 errors/0 warnings), delete; passed after culture fix.
- [x] `COMPILEPLC` on smoke PLC; Success, 0 errors, 0 warnings.
- [x] `UPDATEPROGRAM` on smoke PLC; succeeded.
- [x] `RENAMEPLCBLOCK` disposable FC -> renamed FC, inspected JSON/XREF, compiled (0/0), deleted.
- [x] `DELETEPLCBLOCK` tested on disposable FC/FB/DB/renamed FC.
- [x] Exercised all four filters for an unused PLC tag (`BM_NOP_DI_RDY`) and an unreferenced disposable Bool. `ObjectsWithReferences` returned matches for used tags (`CC_NOP_DI_INI`, `NO_NOP_DO_ALM`) and block `CTRL_MOT`; `ObjectsWithoutReferences`/`UnusedObjects` returned no matches for `CTRL_MOT` and returned the expected unused tag. `AllObjects` and `ObjectsWithReferences` for CTRL_MOT succeeded (large ~108 KB response).

### Block-create culture defect — repaired and live-verified
- [x] Initial `CREATEFC` failed because template `en-US` was not configured in the Spanish-default V21 project.
- [x] Confirmed project culture as `es-ES` from TIA's exported Main XML; ProjectInfo confirms TIA V21 Update 2 Hotfix 1.
- [x] Repaired `ImportPlcBlockTemplate` to use the project's `LanguageSettings.EditingLanguage.Culture` (falling back to an active project language) and substitute every template `<Culture>` element. Updated CREATEFB/CREATEFC/CREATEDB callers.
- [x] Rebuilt bridge and live-tested `CREATEFC`, `CREATEFB`, and `CREATEDB` in root. Each created the requested block; each compiled with 0 errors/0 warnings; each test block was deleted afterward.
- Source files changed by repair: `TiaLocalBridge/Commands/CommandSupport.cs`, `CreateFbCommand.cs`, `CreateFcCommand.cs`, and `CreateDbCommand.cs`.

## Stage 3 — Hardware/catalog/address operations

Use a disposable test fixture (smoke project or copied drive-test clone), and only attempt module placement after inspecting catalog and slot results. Do not modify live machine configurations.

- [x] `SEARCHHWCATALOG` for CPU, DI, and DQ. DI identifier `OrderNumber:6ES7 131-6BF00-0BA0/V1.1`; DQ identifier `OrderNumber:6ES7 132-6BF00-0BA0/V1.1`.
- [x] `GETPLUGLOCATIONS`: root-target rack resolution fixed and live-tested; available locations omit occupied slots.
- [x] `ADDMODULE`: inserted DI at slot 2 and DQ at slot 3; `GETDEVICEITEMS` confirms refs `0/2`, `0/2/1`, `0/3`, `0/3/1`. Follow-up DI at slot 4 returned reusable ref `0/4` after fixing proxy-reference fallback.
- [x] `GETHWPROPERTIES` on CPU and DI; `SETHWPROPERTY` Comment set/verified by returned old/new value. Attempted blank reset was rejected as missing argument; smoke project is disposable and Comment remains `V21 bridge smoke test` on DI.
- [x] `GETHWADDRESSES`: DI Input 0/8 and DQ Output 0/8.
- [x] `SETHWADDRESS`: DI input moved 0 -> 16, verified, restored 16 -> 0.
- [x] On cloned ET200SP rack, `ADDMODULE` rejected an invalid catalog type at valid free slot 12 and a known DI type at invalid slot 299. Follow-up `GETDEVICEITEMS` showed no partial modules.
- [-] No `REMOVEMODULE` command is registered in `HELP`; hardware modules added in the smoke fixture remain there. Keep this project disposable; no safe bridge cleanup command is available.

## Stage 4 — Project networking and PROFINET

Use disposable project, no physical connection/download.

- [x] `CREATESUBNET|Tia21BridgeSmoke_20260617|PN_Smoke_20260617` succeeded.
- [x] `CONNECTTOSUBNET` on PLC and IO-device interfaces succeeded; `GETNODEPROPERTIES` confirms both report a ConnectedSubnet object.
- [x] `SETNODEPROPERTY` Address tested on master: 192.168.0.1 -> 10.178.0.2, verified by `GETIPADDRESS`, then restored to 192.168.0.1 and reverified.
- [x] `CONNECTPROFINET` succeeded with PLC_1 as master, ET200SP_Slave as IO device, exact interface names, and `PN_Smoke_20260617`; bridge reports IO System `PN_Smoke_20260617`. No download/physical connection performed.
- [!] First CONNECTPROFINET attempt failed because the slave interface was not yet connected to the same subnet. The command creates the master IO system before this failure; the retry after `CONNECTTOSUBNET` succeeded and reused that system. Document this precondition/partial-side-effect risk; no source change made.

## Stage 5 — Unified HMI functions

Fixture: disposable clone `20260720_V21/HMI_CC20`; original source project untouched. Test screen/connection objects without bridge delete commands remain in the clone; no HMI download or runtime connection was performed.

- [x] `GETDEVICES`/`GETDEVICESJSON` distinguished PLC software on `ET 200SP station_1` from Unified HMI software on `HMI_CC20`. `GETHMITAGTABLES` and `GETHMITAGS` passed (baseline table count 274).
- [x] `GETHMICONNECTIONS` and `GETHMICONNECTIONPROPERTIES` passed. Existing `HMI_Conexión_1` could be resolved using the unaccented ref `HMI_Conexion_1`. Added `AgentRegressionConnection` with `SIMATIC S7 1200/1500`, no station/partner/node, and `DisabledAtStartup=true`; changed its Comment and restored the original test comment. It remains disabled in the clone.
- [x] HMI tags: `ADDHMITAG`, `ENSUREHMITAG` create/no-op/update, `UPDATEHMITAG`, `GETHMITAGXREF`, and `DELETEHMITAG` passed after fixes. Tested internal Bool/Real/WString/Int tags; all test tags deleted and tag-table count returned to baseline 274. XREF tested all four filters for a test tag and returned expected references for existing `NO_NOP_DO_ALM`.
- [x] Screen groups/screens: `GETHMISCREENGROUPS`, `CREATEHMISCREENGROUP`, `GETHMISCREENS`, `CREATEHMISCREEN`, `GETHMISCREENPROPERTIES`, and `SETHMISCREENPROPERTY` passed on `AgentRegression/HmiRegressionScreen` in new group `AgentRegression`; BackColor set to and verified as RGB 18/52/86.
- [x] Items: `ADDHMISCREENITEM` and `ENSUREHMISCREENITEM` create/no-op/update passed for TEXT, BUTTON, and IOFIELD items. `GETHMISCREENITEMS`/`GETHMISCREENITEMPROPERTIES` passed. Plain text containing `&` and angle brackets normalized/escaped correctly. `SETHMISCREENITEMPROPERTY` set/verified Text and Visible; test item Visible restored to true.
- [x] Bindings: `GETHMISCREENITEMTAGBINDINGS`, `SETHMISCREENITEMTAGBINDING`, and `ENSUREHMISCREENITEMTAGBINDING` passed. Tested TagDynamization on button Visible (restored test binding to `Vis_BtnClose`) and direct IOField ProcessValue (final test binding `PAR_RECETA_CC_VEL_OUT`). Ensure create/no-op/update paths passed.
- [-] Bridge has no HMI connection/screen-group/screen/screen-item delete commands. Test objects remain only in the disposable clone; discard the clone if a clean HMI fixture is needed.

## Stage 6 — Block export/import and JSON variants

- [x] `EXPORTPLCBLOCK`, `EXPORTPLCBLOCKDOCS`, `EXPORTPLCBLOCKSMART`, `EXPORTPLCBLOCKSMARTJSON` on Main all passed.
- [x] Inspected XML and `.s7dcl`; JSON reported Documents mode, Success, paths, block metadata.
- [x] SCL FC XML export passed after compile; exporting the new FC before compile correctly failed with TIA's `Inconsistent blocks ... cannot be exported` error.
- [x] `IMPORTPLCBLOCKSMART` and `IMPORTPLCBLOCKSMARTJSON` passed with FC XML and Main `.s7dcl`; imported objects were compiled successfully afterward and disposable FC removed.
- [x] Override exercised only against disposable FC and Main in the disposable smoke project; both recompiled. Never override existing user content.
- [x] Tested a missing XML path, a directory containing two `.s7dcl` files, and malformed XML via smart text/JSON imports. Each returned a clean error; post-failure `GETPLCBLOCKS` showed no imported/partial test block.

## Stage 7 — Drive functions and unavailable capabilities

- [x] `GETDRIVEOBJECTS` on smoke PLC_1 and ET200SP_Slave returns a clean no-drive result.
- [!] Drive catalog search found G120C PN and CU240E-2 PN entries, but `ADDDEVICE` for `OrderNumber:6SL3210-1KE11-8AF2/4.7.14` failed (`DeviceComposition.CreateWithItem` and fallback `Create`); `GETDEVICES` verified no partial device. This was not a blocker because the Startdrive clone already has drive objects.
- [x] Closed the smoke project and opened clone `20260720_V21.ap21`. `GETDEVICES` found 11 SINAMICS devices (G120C-2/G120S-2) plus an ET200SP and HMI; `GETDEVICEITEMS` on G_13 located its drive control unit at item `0/0`.
- [x] `GETDRIVEOBJECTS` succeeded on all 11 drives after the fallback fix; each reported SafetyTelegram 30 and MainTelegram 352 with Input/Output addresses and lengths. Sample G_13 original: Safety Input/Output 1152, Len 48; Main Input/Output 1140, Len 96. G120S sample G_7: Safety 1032/48, Main 1020/96.
- [x] `GETDRIVETELEGRAMS` succeeded for G_13 and G_7 using item `0/0`.
- [x] `SETDRIVETELEGRAMNUMBER` tested no-op 352, changed G_13 MainTelegram 352 -> 1 (Len 32), then restored 1 -> 352; final GET confirms the original 352/Len 96 and original addresses.
- [x] `SETDRIVETELEGRAMADDRESS` same-value write 1140 passed. Attempt to use 1141 was correctly rejected as occupied (TIA reported next free 1218); set G_13 Main Input 1140 -> 1218 -> restored 1140. Final GET confirms Input/Output 1140/Len 96 and Safety 1152/Len 48 unchanged.
- [!] V21 Startdrive `DriveObject.DriveObjectNumber` getter throws `Drive object number could not be retrieved` on all tested drives, even though each exposes its telegram collection. Repaired `DriveCommandSupport` to catch this property failure and format it as `<unavailable>`; rebuilt and verified GETDRIVEOBJECTS, GETDRIVETELEGRAMS, and both setters live. Explicit number-based selection remains unavailable for any item that exposes multiple drive objects while this getter fails; all fixture drive items tested exposed a single drive object.
- [x] Unified HMI regression completed on disposable clone `20260720_V21/HMI_CC20`; details and retained test artifacts are recorded in Stage 5 and the HMI work-log entry.
- No physical connection or device download was performed. All drive setter changes were restored to the recorded original values.

## Stage 8 — Add-in / final validation

- [x] Build TiaPiAddin against V21 succeeded before the block-template source fix (fix affects bridge only).
- [x] Validated AddInConfig uses the V21 publisher namespace matching the installed V21 publisher XSD. Existing publisher log records `SUCCEEDED`; packaged `.addin` artifact is a valid ZIP with expected package entries. No installation directory was touched.
- [x] Rebuilt bridge after block-culture, hardware-reference, HMI localized-name/internal-tag, and multilingual old-value fixes. Reconnected to the open V21 instance and confirmed `LIST` still reports `20260720_V21`; previous hardware-reference test evidence remains valid.
- [x] Reran `HELP` after the final HMI fixes; full command registry returned. `LIST` after each rebuild attached to the existing V21 instance and reported `20260720_V21`.
- [ ] Summarize every registered command as Pass / Fail / Not applicable with evidence; preserve this checklist and note remaining defects.

## Work log

### 2026-06-17 — Initial smoke test (previous agent turn)

- Created project and CPU successfully; bridge connected to TIA.
- `COMPILEPLCBLOCK` for Main succeeded with 0 errors and 0 warnings; block consistency verified.
- Initial `CREATEFC` failed due to `en-US` template culture. Fixed later in this work log by deriving culture from project language settings and verified FC/FB/DB create+compile+delete.
- Project file reports TIA Portal V21 Update 2 Hotfix 1. Test project left open at the path listed above.

### 2026-06-17 — Stage 1–3 tests and culture fix

- `GETDEVICESJSON`: valid JSON; PLC software true, HMI software false. `GETNETWORKINTERFACES`, `GETIPADDRESS`, `GETNODEPROPERTIES`: successful; interface `Interfaz PROFINET_1`, address `192.168.0.1`/`255.255.255.0`.
- `GETHWPROPERTIES` root/CPU/DI successful. DI Comment set to `V21 bridge smoke test`; blank reset rejected because the bridge argument validator drops empty/whitespace arguments. Only the disposable fixture is affected.
- `GETPLUGLOCATIONS|...|DEVICE` initially gave false empty positions. Fixed root-target handling to resolve a single root rack and use its child occupancy. After rebuilding, the root query listed available rack positions starting at 4 and omitted occupied 1–3.
- Hardware catalog found DI/DQ V1.1. Initial ADDMODULE inserted them at slots 2/3 but returned `<position-2>/<position-3>`; fixed proxy-reference fallback, verified follow-up slot-4 module returns reusable `Reference=0/4`. Initial modules remain with reusable refs `0/2`, `0/3`.
- `GETHWADDRESSES`: DI Input 0/8; DQ Output 0/8. `SETHWADDRESS` DI Input changed 0 -> 16, verified, restored to 0.
- `GETDRIVEOBJECTS` on PLC cleanly returned no-drive result. PLC tag ADD/GET/XREF/UPDATE/GET/DELETE passed using accent-insensitive `Tabla de variables estandar`; disposable tag deleted.
- Initial hard-coded `en-US` create-template culture failure repaired dynamically using project EditingLanguage culture (`es-ES` here). FC/FB/DB create, compile (0/0), and delete all passed. Disposable FC rename, JSON info, `UnusedObjects` XREF, compile, and delete passed.
- `COMPILEPLC` passed (0 errors/warnings); `UPDATEPROGRAM` succeeded.
- Bridge and add-in builds passed. First bridge rebuild after source edits failed because running `TiaLocalBridge.exe` was locked; `EXIT`, rebuild, and reattachment fixed workflow.
- Main block XML/docs/smart/smart-JSON exports all passed. XML and `.s7dcl` inspected; JSON reported Documents mode/Success and paths. XML shows culture `es-ES`.

### 2026-06-17 — Stage 3 hardware fixes and Stage 6 import tests

- Fixed `GETPLUGLOCATIONS` for rack-based stations: a `DEVICE` target now resolves its single root `System:Rack` and reports the rack's genuinely available plug positions with correct occupancy. Rebuilt and confirmed positions 1–3 are omitted after CPU/DI/DQ insertion.
- Fixed `ADDMODULE` reusable-reference fallback when Openness returns a different proxy wrapper than enumeration; follow-up slot 4 insertion returned `Reference=0/4`.
- Both changes compiled. Source files: `GetPlugLocationsCommand.cs` and `AddModuleCommand.cs`.
- Created/compiled disposable FC, exported XML, imported with `IMPORTPLCBLOCKSMART` and JSON variant (`overrideExisting=true`), compiled again, then deleted. First export attempt before compile failed as expected for an inconsistent block; compile then export succeeded.
- Imported Main `.s7dcl` with both smart import variants (text/JSON); both succeeded, and Main recompiled with 0 errors/0 warnings.
- Export files remain under `C:\Users\masias\Documents\Automation\Tia21BridgeSmoke_20260617\Exports`.
- Final state check before network stage: project remained open; only Main block remained and was consistent; no PLC tags remained; disposable test modules remained in the project.

### 2026-06-17 — Stage 4 networking

- Refreshed discovery first: PLC_1 ref and `Interfaz PROFINET_1`, original IP 192.168.0.1/24, ConnectedSubnet initially null.
- Created `PN_Smoke_20260617`, connected PLC interface, and verified ConnectedSubnet became non-null while IP remained unchanged.
- Tested `SETNODEPROPERTY` Address to 10.178.0.2; verified readback; restored original 192.168.0.1 and re-read node properties.
- Found IM 155-6 PN ST in catalog (V6.4), added it as disposable device `ET200SP_Slave`; device interface is `Interfaz PROFINET`.
- First `CONNECTPROFINET` call failed because the slave interface was not on the controller's subnet. The first attempt created the master IO system before `ConnectToIoSystem` threw. Connected slave via `CONNECTTOSUBNET`, retried, and received success for IO System `PN_Smoke_20260617` (reusing the system). This confirms both interfaces must share a subnet and reveals partial-side-effect risk on failure; consider a preflight check/help-text improvement later, but avoid implicit connection behavior until tested more broadly.
- Verified both network nodes report ConnectedSubnet and their addresses (master restored to 192.168.0.1, slave default 192.168.0.2). No device download or physical connection.

### 2026-06-17 — Stage 7 drive test attempt

- `GETDRIVEOBJECTS` on both smoke PLC_1 and ET200SP_Slave reports no drive objects.
- Hardware catalog search found G120C PN (`OrderNumber:6SL3210-1KE11-8AF2/4.7.14`) and CU240E-2 PN. Attempted `ADDDEVICE` for G120C PN; Openness `CreateWithItem` and fallback `Create` both failed. Follow-up `GETDEVICES` confirmed no partial device addition.
- Found an existing V21 project whose `ProjectInfo.txt` includes Startdrive G120/G120C V21 HF2. Copied it (157 MB) to `C:\Users\masias\Documents\Automation\Tia21DriveRegressionClone`; original left untouched.
- `OPEN` of the clone failed with TIA error: `Another project is already open.` The disposable smoke project remains open, and the bridge offers no close-project command. No drive telegram mutation was attempted. Resume by closing the smoke project in the TIA UI, then opening the staged clone and discovering its drive objects.

### 2026-07-20 — Stage 7 drive regression completed

- `GETDEVICES` on clone `20260720_V21` found 11 drives: G120C-2 and G120S-2. `GETDEVICEITEMS|.../SINAMICS G_13` showed the drive object container at `0/0`; no HMI APIs were called despite HMI_CC20 being present.
- Initial `GETDRIVEOBJECTS` failed on `DriveObject.DriveObjectNumber` with “Drive object number could not be retrieved.” Repaired `DriveCommandSupport` with a safe getter/fallback so an unreadable object number no longer prevents listing telegrams; all 11 drives then listed successfully. Rebuilt with `dotnet msbuild TiaLocalBridge/TiaLocalBridge.csproj -t:Build -p:Configuration=Debug`.
- On G_13, original configuration: SafetyTelegram 30 at Input/Output 1152, Len 48; MainTelegram 352 at Input/Output 1140, Len 96. `GETDRIVETELEGRAMS` also passed on G_7 (G120S).
- `SETDRIVETELEGRAMNUMBER`: same-value 352 passed as unchanged; changed MainTelegram 352 -> 1, observed Len 32, then restored to 352 and verified Len 96 and original addresses.
- `SETDRIVETELEGRAMADDRESS`: same-value Input 1140 passed. Input 1141 was rejected because occupied; TIA reported next free address 1218. Changed Input 1140 -> 1218, verified, restored 1218 -> 1140, then verified the complete original G_13 configuration. No other telegram addresses changed.
- Drive object number still displays `<unavailable>` because the V21 getter itself fails. All tested containers had one drive object; if future items expose multiple objects and numbers remain unreadable, selection by explicit number remains blocked. No downloads/physical connection.
- Re-ran `LIST` (`20260720_V21`) and `HELP`; both passed. HMI regression remains deferred to a separate session.

### 2026-07-20 — Follow-up protocol and add-in checks

- Sent read-only negative cases against the open drive clone: unknown command, missing/extra `GETDRIVEOBJECTS` arguments, nonexistent device reference, nonexistent device-item reference, and nonexistent `GETDEVICESJSON` reference. Errors were framed as normal JSON responses with `status:error`; successful drive discovery retained the normal text-success envelope.
- Validated `TiaPiAddin/AddInConfig.xml` namespace against the installed V21 publisher XSD. Existing `publisher-v21.log` reports `SUCCEEDED`; the local `.addin` package has the expected ZIP package structure. Did not install or overwrite any TIA Add-in.

### 2026-07-20 — Additional PLC, XREF, hardware-validation, and import-error checks

- Read-only PLC discovery on the cloned ET200SP fixture passed: `GETDEVICESJSON` reports PLC software and no HMI software on that device; tag tables and block groups listed successfully. `GETPLCBLOCKS` reports the pre-existing `CTRL_MOT` FB as inconsistent; did not compile or modify it.
- Created Bool/Byte/Word/DInt/Real marker tags at `%M300.0`, `%MB301`, `%MW302`, `%MD304`, `%MD308`; changed the Byte test tag to Word `%MW312`, verified through `GETPLCTAGS`, deleted all five, and confirmed table count returned to baseline. No I/Q or drive addresses were touched.
- PLC tag XREF filters returned expected results on used and unused tags. Block XREF AllObjects/ObjectsWithReferences on `CTRL_MOT` succeeded but produced a large (~108 KB) response; no block changes were made. One PLC-tag XREF naturally included HMI references from the project; no HMI-specific command or mutation was performed.
- ADDMODULE invalid-type and invalid-slot attempts were rejected by `CanPlugNew`; a follow-up device-item listing confirmed no partial hardware.
- Import-error checks: nonexistent XML path and a two-`.s7dcl` directory returned source-resolution errors before import. Malformed XML reached TIA and failed with an XML parse error; subsequent block listing showed no test/partial block.
- For the localized default tag table, pass `Tabla de variables estandar` without the accent through the bridge; the literal accented command argument was not resolved in this run (console output shows mojibake), while the accentless form succeeded.
- Deleted the temporary malformed-import test files from `%LOCALAPPDATA%\Temp\TiaV21BridgeRegressionImportCases` after the checks.

### 2026-07-20 — Unified HMI regression

- Read-only discovery: `HMI_CC20` reports `hasHmiSoftware=true`, `hasPlcSoftware=false`; HMI tag table count was 274. Listed the 274 tags, one configured HMI connection, screen groups, screens, and representative screen/item properties. Existing screen content was not changed.
- Localized refs initially blocked the tag-table and connection lookups when supplied with accents. Added accent-insensitive fallbacks to `ResolveUnifiedHmiTagTable` and `ResolveUnifiedHmiConnection`; rebuilt and attached to the still-open V21 project. ASCII refs `Tabla de variables estandar` and `HMI_Conexion_1` then succeeded. Literal accented command args still fail through this bridge input path; README now documents the ASCII workaround.
- `GETHMITAGXREF` on existing `NO_NOP_DO_ALM` returned references from the HMI `02_Header` script, PLC block `ALM_BC`, PLC tag `NO_NOP_DO_ALM`, and connection/cycle dependencies. All four filters were exercised on a disposable tag.
- First `ADDHMITAG` for an internal Bool with `-|-` returned a generic TIA invocation error but had already created the tag (table count 275). `ENSUREHMITAG` with the same empty optional fields also errored. Repaired `SetOptionalUnifiedHmiTagTextProperty` to avoid writing already-empty/internal connection fields and taught `ENSUREHMITAG` to normalize localized `<Variable interna>`. Rebuilt and verified a new `ADDHMITAG` succeeds, Ensure create/no-op/update, `UPDATEHMITAG`, XREF, and `DELETEHMITAG`; removed both tags from the failed and successful attempts and confirmed the baseline count returned to 274.
- Created test-only group `AgentRegression`, screen `AgentRegression/HmiRegressionScreen`, and items `AgentTitle` (TEXT), `AgentBoundButton` (BUTTON), and `AgentIoField` (IOFIELD). Tested create/list/properties, `ENSUREHMISCREENITEM` create/no-op/update, multilingual text escaping, BackColor conversion/readback, Visible toggle and restore, direct ProcessValue binding and TagDynamization binding. Test screen’s final binding values are `Vis_BtnClose` and `PAR_RECETA_CC_VEL_OUT` respectively.
- Added `AgentRegressionConnection` using the SIMATIC S7 1200/1500 driver with no Station/Partner/Node and `DisabledAtStartup=true`. `SETHMICONNECTIONPROPERTY` Comment was changed and restored; the connection remains disabled and disconnected in the clone. No download or runtime connection.
- Observed `SETHMISCREENITEMPROPERTY|...|Text` reported identical OldValue/NewValue because `MultilingualText` was mutated in place. Fixed the command helper to snapshot the formatted old value before mutation; rebuilt and verified it reports distinct old/new text.
- Test HMI tags were deleted. The test group, screen, items, and disabled connection remain in the disposable clone because no delete commands are registered; discard the clone for cleanup. Source project remains untouched.
