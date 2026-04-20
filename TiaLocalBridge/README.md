# TiaLocalBridge

A small command bridge for working with **TIA Portal** projects from a console process using the **Siemens Openness / Public API**.
all the references to PackagingMachine project is a SIEMENS example project : https://support.industry.siemens.com/cs/document/109997591/dual-bag-packaging-machine?dti=0&lc=en-bg
This tool accepts pipe-separated commands such as:

```text
GETDEVICES|PackagingMachine
GETDEVICEITEMS|PackagingMachine/S7-1500/ET200MP station_1
GETPLCTAGTABLES|PackagingMachine/S7-1500/ET200MP station_1
GETPLCBLOCKS|PackagingMachine/S7-1500/ET200MP station_1
```

It returns JSON lines with either:
- `event`
- `response`
- `fatal`

Example response:

```json
{"type":"response","status":"success","command":"GETDEVICES","portalConnected":true,"resultType":"text","result":"Project 'PackagingMachine' devices: ..."}
```

---

## Purpose

This project is meant to make it easier to automate TIA Portal tasks such as:
- opening projects
- listing devices
- discovering PLC modules
- reading HMI and PLC tags
- creating, updating, and deleting PLC tags
- discovering PLC blocks
- exporting PLC blocks
- importing PLC blocks from XML or document exports

tha main purpose is to research the use of it by AI agents to interact with TIA portal
---

## Requirements

- Windows
- TIA Portal installed
- Siemens Public API / Openness DLL available
- .NET Framework 4.8
- A TIA Portal instance running, or permission for the bridge to start one (Check TIA openess documentaion)

This project currently references:
- `Siemens.Engineering.dll`

---

## Build

Example build command:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' 'C:\Users\masias\Documents\Workspace2\TiaLocalBridge\TiaLocalBridge.csproj' /t:Build /p:Configuration=Debug /nologo
```

Output executable:

```text
TiaLocalBridge/bin/Debug/TiaLocalBridge.exe
```

---

## Command protocol

Commands are sent as a single line:

```text
COMMAND|arg1|arg2|arg3
```

Special command:

```text
EXIT
```

The bridge prints JSON lines to stdout.

---

## Available commands

### Project commands
- `OPEN|<project-file-path>`
- `CREATE|<target-directory>|<project-name>`
- `LIST`
- `HELP`

### Device commands
- `SEARCHHWCATALOG|<filter>|[max-results]`
- `ADDDEVICE|<project-name>|<type-identifier>|<device-name>|[device-item-name]`
- `ADDMODULE|<device-reference>|<parent-target-reference>|<type-identifier>|<module-name>|<position-number>`
- `GETDEVICES|[project-name]`
- `GETDEVICESJSON|<device-reference>`
- `GETDEVICEITEMS|<device-reference>`
- `GETPLUGLOCATIONS|<device-reference>|<target-reference>`
- `GETHWPROPERTIES|<device-reference>|<target-reference>`
- `SETHWPROPERTY|<device-reference>|<target-reference>|<property-name>|<value>`
- `GETHWADDRESSES|<device-reference>|<target-reference>`
- `SETHWADDRESS|<device-reference>|<target-reference>|<io-type>|<start-address>`

Example device and module workflow:

```text
SEARCHHWCATALOG|1510SP-1 PN|10
ADDDEVICE|DemoProject|OrderNumber:6ES7 510-1DJ01-0AB0/V3.0|PLC_1|PLC_1
GETDEVICES|DemoProject
GETDEVICEITEMS|DemoProject/PLC_1
SEARCHHWCATALOG|6ES7 131-6BF00|5
ADDMODULE|DemoProject/PLC_1|0|OrderNumber:6ES7 131-6BF00-0BA0/V1.1|DI_8x24VDC_ST_1|2
GETHWPROPERTIES|DemoProject/PLC_1|0/2
GETHWADDRESSES|DemoProject/PLC_1|0/2/1
SETHWADDRESS|DemoProject/PLC_1|0/2/1|Input|8
```

### Drive telegram commands
- `GETDRIVEOBJECTS|<device-reference>`
- `GETDRIVETELEGRAMS|<device-reference>|<device-item-reference>|[drive-object-number]`
- `SETDRIVETELEGRAMNUMBER|<device-reference>|<device-item-reference>|<telegram-type>|<telegram-number>|[drive-object-number]`
- `SETDRIVETELEGRAMADDRESS|<device-reference>|<device-item-reference>|<telegram-type>|<io-type>|<start-address>|[drive-object-number]`

These commands expose the Siemens MC Drives telegram APIs that were previously explored through direct PowerShell scripts. Use `GETDEVICEITEMS` or `GETDRIVEOBJECTS` first to find the drive-capable item reference, then inspect or change the drive object's telegram numbers and telegram IO start addresses through the bridge.

### Network commands
- `CREATESUBNET|<project-name>|<subnet-name>`
- `GETNETWORKINTERFACES|<device-reference>`
- `GETIPADDRESS|<device-reference>|<interface-name>`
- `GETNODEPROPERTIES|<device-reference>|<interface-name>`
- `SETNODEPROPERTY|<device-reference>|<interface-name>|<property-name>|<value>`
- `CONNECTTOSUBNET|<device-reference>|<interface-name>|<subnet-name>`
- `CONNECTPROFINET|<master-device-ref>|<master-interface-name>|<slave-device-ref>|<slave-interface-name>|<subnet-name>`

### PLC tag commands
- `GETPLCTAGTABLES|<device-reference>`
- `GETPLCTAGS|<device-reference>`
- `GETPLCTAGXREF|<device-reference>|<table-reference>|<tag-name>|[filter]`
- `ADDPLCTAG|<device-reference>|<table-reference>|<tag-name>|<data-type>|<logical-address>`
- `UPDATEPLCTAG|<device-reference>|<table-reference>|<tag-name>|<data-type>|<logical-address>`
- `DELETEPLCTAG|<device-reference>|<table-reference>|<tag-name>`

PLC tag table resolution now also tolerates accent-insensitive names, which is useful when the default table name contains localized characters such as `estándar`.

### PLC block commands
- `GETPLCBLOCKGROUPS|<device-reference>`
- `GETPLCBLOCKS|<device-reference>`
- `GETPLCBLOCKSJSON|<device-reference>`
- `GETPLCBLOCKXREF|<device-reference>|<block-reference>|[filter]`
- `GETPLCBLOCKINFO|<device-reference>|<block-reference>`
- `GETPLCBLOCKINFOJSON|<device-reference>|<block-reference>`
- `CREATEFB|<device-reference>|<target-group-reference>|<block-name>`
- `CREATEFC|<device-reference>|<target-group-reference>|<block-name>`
- `CREATEDB|<device-reference>|<target-group-reference>|<block-name>`
- `DELETEPLCBLOCK|<device-reference>|<block-reference>`
- `RENAMEPLCBLOCK|<device-reference>|<block-reference>|<new-block-name>`
- `COMPILEPLCBLOCK|<device-reference>|<block-reference>`
- `COMPILEPLC|<device-reference>`
- `UPDATEPROGRAM|<device-reference>`
- `EXPORTPLCBLOCK|<device-reference>|<block-reference>|<target-file-path>`
- `EXPORTPLCBLOCKDOCS|<device-reference>|<block-reference>|<target-directory>|[file-name-without-extension]`
- `EXPORTPLCBLOCKSMART|<device-reference>|<block-reference>|<target-directory>`
- `EXPORTPLCBLOCKSMARTJSON|<device-reference>|<block-reference>|<target-directory>`
- `IMPORTPLCBLOCKSMART|<device-reference>|<source-path>|[target-group-reference]`
- `IMPORTPLCBLOCKSMARTJSON|<device-reference>|<source-path>|[target-group-reference]`

### HMI commands
- `GETHMITAGS|<device-reference>`
- `GETHMITAGTABLES|<device-reference>`
- `GETHMITAGXREF|<device-reference>|<table-reference>|<tag-name>|[filter]`
- `ADDHMITAG|<device-reference>|<table-reference>|<tag-name>|<data-type>|<address>|<connection>`
- `ENSUREHMITAG|<device-reference>|<table-reference>|<tag-name>|<data-type>|<address>|<connection>`
- `UPDATEHMITAG|<device-reference>|<table-reference>|<tag-name>|<data-type>|<address>|<connection>`
- `DELETEHMITAG|<device-reference>|<table-reference>|<tag-name>`
- `GETHMICONNECTIONS|<device-reference>`
- `GETHMICONNECTIONPROPERTIES|<device-reference>|<connection-name>`
- `ADDHMICONNECTION|<device-reference>|<connection-name>|<communication-driver>|<initial-address>|<disabled-at-startup>|<comment>`
- `SETHMICONNECTIONPROPERTY|<device-reference>|<connection-name>|<property-name>|<value>`
- `GETHMISCREENGROUPS|<device-reference>`
- `CREATEHMISCREENGROUP|<device-reference>|<parent-group-reference>|<group-name>`
- `GETHMISCREENS|<device-reference>`
- `CREATEHMISCREEN|<device-reference>|<parent-group-reference>|<screen-name>`
- `GETHMISCREENPROPERTIES|<device-reference>|<screen-reference>`
- `SETHMISCREENPROPERTY|<device-reference>|<screen-reference>|<property-name>|<value>`
- `GETHMISCREENITEMS|<device-reference>|<screen-reference>`
- `GETHMISCREENITEMPROPERTIES|<device-reference>|<screen-reference>|<item-name>`
- `GETHMISCREENITEMTAGBINDINGS|<device-reference>|<screen-reference>|<item-name>`
- `ADDHMISCREENITEM|<device-reference>|<screen-reference>|<item-type>|<item-name>|<left>|<top>|<width>|<height>|[text]`
- `ENSUREHMISCREENITEM|<device-reference>|<screen-reference>|<item-type>|<item-name>|<left>|<top>|<width>|<height>|[text]`
- `SETHMISCREENITEMPROPERTY|<device-reference>|<screen-reference>|<item-name>|<property-name>|<value>`
- `SETHMISCREENITEMTAGBINDING|<device-reference>|<screen-reference>|<item-name>|<target-property>|<tag-name>`
- `ENSUREHMISCREENITEMTAGBINDING|<device-reference>|<screen-reference>|<item-name>|<target-property>|<tag-name>`

---

## Important behavior discovered during this session

This section documents the most important findings from testing against the example project **PackagingMachine**.

### 1. Device names can contain `/`

A device name such as:

```text
S7-1500/ET200MP station_1
```

caused ambiguity because `/` was also used as a path separator in project-qualified references.

### Solution implemented
`GETDEVICES` now returns a clear reusable reference:

```text
Project 'PackagingMachine' devices: S7-1500/ET200MP station_1 [Reference=PackagingMachine/S7-1500/ET200MP station_1, Type=System:Device.S71500], HMI DualBag [Reference=PackagingMachine/HMI DualBag, Type=]
```

Use the `Reference=...` value with commands like:
- `GETDEVICEITEMS`
- `GETPLCTAGS`
- `GETPLCTAGTABLES`
- `GETPLCBLOCKS`
- `GETPLCBLOCKSJSON`
- `GETPLCBLOCKINFO`
- `GETPLCBLOCKINFOJSON`

---

### 2. `GETDEVICEITEMS` is useful to list PLC modules

The help and descriptions were updated so it is clear that:
- `GETDEVICES` gives the device reference
- `GETDEVICEITEMS` lists the device items / PLC modules

Example:

```text
GETDEVICEITEMS|PackagingMachine/S7-1500/ET200MP station_1
```

`GETDEVICEITEMS` now returns reusable `Reference=...` values for each device item/module using a position-path format such as `1` or `1/4`. These references can be used with:
- `GETPLUGLOCATIONS`
- `ADDMODULE`
- `GETHWPROPERTIES`
- `SETHWPROPERTY`

---

### 2b. Hardware modules and hardware properties can now be manipulated directly

The bridge now supports a first direct hardware workflow for PLC stations and racks:
- inspect device items/modules with stable item references
- inspect available plug positions on a device or parent module
- plug new modules from the hardware catalog into a slot
- inspect hardware properties exposed by Openness on devices and modules
- set writable hardware properties on devices and modules
- inspect IO address objects exposed by Openness
- change IO start addresses for module subslots that expose address objects

### Commands added
- `GETPLUGLOCATIONS`
- `ADDMODULE`
- `GETHWPROPERTIES`
- `SETHWPROPERTY`
- `GETHWADDRESSES`
- `SETHWADDRESS`

### Practical workflow
1. `GETDEVICES`
2. `GETDEVICEITEMS`
3. `SEARCHHWCATALOG`
4. `ADDMODULE`
5. `GETHWPROPERTIES`
6. `SETHWPROPERTY`
7. `GETHWADDRESSES`
8. `SETHWADDRESS`

### Example

```text
GETDEVICEITEMS|agenthwtest0325a/PLC_1510SP1_PN
SEARCHHWCATALOG|6ES7 131-6BF00|5
ADDMODULE|agenthwtest0325a/PLC_1510SP1_PN|0|OrderNumber:6ES7 131-6BF00-0BA0/V1.1|DI_8x24VDC_ST_1|2
GETHWPROPERTIES|agenthwtest0325a/PLC_1510SP1_PN|0/2
GETHWADDRESSES|agenthwtest0325a/PLC_1510SP1_PN|0/2/1
SETHWADDRESS|agenthwtest0325a/PLC_1510SP1_PN|0/2/1|Input|8
```

Important notes:
- for device-level hardware properties use target reference `DEVICE`
- for module-level hardware properties use the exact `Reference=...` returned by `GETDEVICEITEMS`
- `SETHWPROPERTY` works only for properties exposed by Openness as writable attributes
- `GETHWADDRESSES` and `SETHWADDRESS` target the Openness address objects that correspond to the TIA Portal I/O addresses page
- for ET200SP modules tested here, the usable address object was exposed on the module subslot (for example `0/2/1`), not on the module root (`0/2`)
- actual writable property names and supported value types depend on the specific device/module and TIA version

---

### 3. PLC tag management can be done directly with Openness

Export/edit/import is **not required** for basic PLC tag management.

The TIA API supports direct PLC tag operations:
- create
- update
- delete

### Commands added
- `GETPLCTAGTABLES`
- `ADDPLCTAG`
- `UPDATEPLCTAG`
- `DELETEPLCTAG`

### Practical workflow
1. `GETDEVICES`
2. `GETPLCTAGTABLES`
3. `GETPLCTAGS`
4. `ADDPLCTAG` / `UPDATEPLCTAG` / `DELETEPLCTAG`

### Example

```text
GETPLCTAGTABLES|PackagingMachine/S7-1500/ET200MP station_1
ADDPLCTAG|PackagingMachine/S7-1500/ET200MP station_1|02_Global/Global|MyNewTag|Bool|%M20.0
UPDATEPLCTAG|PackagingMachine/S7-1500/ET200MP station_1|02_Global/Global|MyNewTag|Bool|%M20.1
DELETEPLCTAG|PackagingMachine/S7-1500/ET200MP station_1|02_Global/Global|MyNewTag
```

---

### 4. PLC blocks should be discovered before trying to modify content

We first added block discovery and metadata commands before editing content.

### Commands added
- `GETPLCBLOCKGROUPS`
- `GETPLCBLOCKS`
- `GETPLCBLOCKINFO`

These commands expose:
- group/folder structure
- block reference
- block type
- programming language
- block number
- know-how protection flag
- timestamps
- instance DB relationship

### Supported block info categories observed
- `OB`
- `FC`
- `FB`
- `DB`
- `InstanceDB`

### Languages observed
- `LAD`
- `SCL`
- `DB`
- also system/library blocks using other languages may appear

---

### 5. Block export format depends on type and language

This was one of the most important findings of the session.

We tested the following block kinds:

#### LAD block
- Block: `01_CentralFunctions/Main`
- Type: `OB`
- Language: `LAD`

Results:
- `EXPORTPLCBLOCK` -> XML export works
- `EXPORTPLCBLOCKDOCS` -> produced:
  - `.s7dcl`
  - `.s7res`

Findings:
- XML contains Openness XML with compile units and network source
- document export is very readable for LAD blocks
- `.s7res` stores multilingual resource text

#### SCL block
- Block: `04_ObjectsOfProjectLibraryUsed/ClearAlarms`
- Type: `FC`
- Language: `SCL`

Results:
- `EXPORTPLCBLOCK` -> XML export works
- XML contains `StructuredText` tokens and actual code structure
- `EXPORTPLCBLOCKDOCS` failed with:
  - `The block 'ClearAlarms' is know-how-protected and cannot be exported.`

Additional finding:
- `GETPLCBLOCKINFO` showed `KnowHowProtected=False`
- but document export still failed as if protected
- another SCL FC showed the same behavior

Conclusion:
- **SCL is safest to export as XML** in this project/environment

#### Global DB
- Block: `02_Global/Data`
- Type: `DB`
- Language: `DB`

Results:
- XML export works
- document export works
- document export produced:
  - `.s7dcl`
- no `.s7res` was created

Conclusion:
- DB blocks are easy to inspect through `.s7dcl`

#### Instance DB
- Block: `01_CentralFunctions/00_General/Instance DBs/InstCallGeneral`
- Type: `InstanceDB`
- Language: `DB`

Results:
- XML export works
- document export works
- document export produced:
  - `.s7dcl`

Conclusion:
- instance DBs are also easy to inspect through `.s7dcl`

---

### 6. Smart export strategy was implemented

Because different block types behave differently, `EXPORTPLCBLOCKSMART` was added.

### Smart export rules currently used
- `SCL` -> XML
- `DB` -> documents
- `InstanceDB` -> documents
- `LAD/FBD/other non-SCL blocks` -> documents
- if document export fails -> fallback to XML

### Example

```text
EXPORTPLCBLOCKSMART|PackagingMachine/S7-1500/ET200MP station_1|01_CentralFunctions/Main|C:\Exports
```

### Verified live
- LAD -> documents
- SCL -> XML
- DB -> documents
- InstanceDB -> documents

---

### 7. Smart import was implemented and tested successfully

`IMPORTPLCBLOCKSMART` was added to automatically detect the source format and call the correct import method.

### Supported input sources
- `.xml`
- `.s7dcl`
- directory containing exactly one `.s7dcl`

### Import behavior
- imports with **override**
- optional target block group
- default target group is `<root>`

### Real test performed in this session
We picked a non-OB block from the example project:
- original block: `02_Global/Data`
- type: `DB`

We created a renamed document file:
- new name: `Data_ImportedTest`

Then imported with:

```text
IMPORTPLCBLOCKSMART|PackagingMachine/S7-1500/ET200MP station_1|C:\Users\masias\Documents\Workspace2\TiaLocalBridge\imports-test\Data_ImportedTest.s7dcl|02_Global
```

Result:
- import succeeded
- new block created:
  - `02_Global/Data_ImportedTest`
- reported as:
  - `Type=DB`
  - `Language=DB`

### Important post-import finding
After import, `GETPLCBLOCKINFO` showed:
- `IsConsistent=False`
- `CompileDate=0001-01-01...`

Conclusion:
- import works
- but imported blocks may still need compile/update actions in TIA Portal

---

### 8. Direct `CreateFB(...)` did not work for normal FB languages in this environment

We tested Openness FB creation directly against the example project.

Languages tested:
- `SCL`
- `LAD`
- `FBD`
- `STL`
- `GRAPH`
- `ProDiag`

Observed result:
- `SCL`, `LAD`, `FBD`, `STL`, `GRAPH` -> failed
- `ProDiag` -> succeeded

The common error for the non-ProDiag cases was:

```text
The action "Create block" only supports the programming language 'ProDiag'.
```

Conclusion:
- in this tested TIA V20 / PackagingMachine environment, direct Openness `PlcBlockComposition.CreateFB(...)` was not suitable for creating a normal disposable SCL FB
- the bridge therefore should not rely on direct `CreateFB(..., ProgrammingLanguage.SCL)` here

---

### 9. `CREATEFB` now works through XML template import

To keep a safe disposable FB creation workflow, `CREATEFB` was changed to:
- load a built-in minimal SCL FB XML template
- replace the block name
- assign the next available FB number
- import the customized XML into the requested target block group

### Verified live
Command used:

```text
CREATEFB|PackagingMachine/S7-1500/ET200MP station_1|02_Global|Fb_AgentTest_...
```

Observed result:
- create succeeded
- created block type: `FB`
- programming language: `SCL`
- target group: `02_Global`
- initial state after creation:
  - `IsConsistent=False`
  - `CompileDate=0001-01-01...`

After running:

```text
COMPILEPLCBLOCK|PackagingMachine/S7-1500/ET200MP station_1|02_Global/Fb_AgentTest_...
```

Observed result:
- compile succeeded
- block became:
  - `IsConsistent=True`
  - compiled date populated

We also verified cleanup with:

```text
DELETEPLCBLOCK|PackagingMachine/S7-1500/ET200MP station_1|02_Global/Fb_AgentTest_...
```

Conclusion:
- the current `CREATEFB` command is now usable again in this repo
- it should be treated like an import-style create, so compile/update follow-up may still be needed afterward

### 10. `CREATEFC` now creates a disposable SCL FC through XML template import

`CREATEFC` follows the same practical strategy as `CREATEFB`:
- load a built-in minimal SCL FC XML template
- replace the block name
- assign the next available FC number
- import the customized XML into the requested target block group

Expected workflow:

```text
CREATEFC|PackagingMachine/S7-1500/ET200MP station_1|02_Global|Fc_AgentTest
GETPLCBLOCKINFO|PackagingMachine/S7-1500/ET200MP station_1|02_Global/Fc_AgentTest
COMPILEPLCBLOCK|PackagingMachine/S7-1500/ET200MP station_1|02_Global/Fc_AgentTest
```

Like imported blocks and template-created FBs, created FCs may still require `COMPILEPLCBLOCK`, `COMPILEPLC`, or `UPDATEPROGRAM` afterward.

### 11. `CREATEDB` now creates a disposable global DB through XML template import

`CREATEDB` follows the same minimal-template strategy for global DB creation:
- load a built-in minimal global DB XML template
- replace the block name
- assign the next available DB number
- import the customized XML into the requested target block group

Expected workflow:

```text
CREATEDB|PackagingMachine/S7-1500/ET200MP station_1|02_Global|Db_AgentTest
GETPLCBLOCKINFO|PackagingMachine/S7-1500/ET200MP station_1|02_Global/Db_AgentTest
COMPILEPLCBLOCK|PackagingMachine/S7-1500/ET200MP station_1|02_Global/Db_AgentTest
```

Implementation notes:
- DB number allocation avoids collisions with existing global DBs and instance DBs
- like other template-import creates, the new DB may still require `COMPILEPLCBLOCK`, `COMPILEPLC`, or `UPDATEPROGRAM` afterward
- build and `HELP` were verified after adding the command; a dedicated live project test should still be preferred before relying on it for larger workflows

---

## Recommended usage patterns

### Discover PLC modules
```text
GETDEVICES|PackagingMachine
GETDEVICEITEMS|PackagingMachine/S7-1500/ET200MP station_1
```

### Add hardware modules and inspect properties
```text
GETDEVICEITEMS|agenthwtest0325a/PLC_1510SP1_PN
SEARCHHWCATALOG|6ES7 131-6BF00|5
ADDMODULE|agenthwtest0325a/PLC_1510SP1_PN|0|OrderNumber:6ES7 131-6BF00-0BA0/V1.1|DI_8x24VDC_ST_1|2
SEARCHHWCATALOG|6ES7 132-6BF00|5
ADDMODULE|agenthwtest0325a/PLC_1510SP1_PN|0|OrderNumber:6ES7 132-6BF00-0BA0/V1.1|DQ_8x24VDC_ST_1|3
GETHWPROPERTIES|agenthwtest0325a/PLC_1510SP1_PN|0/2
GETHWPROPERTIES|agenthwtest0325a/PLC_1510SP1_PN|0/3
```

### Change a hardware property
```text
SETHWPROPERTY|agenthwtest0325a/PLC_1510SP1_PN|0/1|Comment|Bridge test comment
```

### Read and change module IO addresses
```text
GETHWADDRESSES|agenthwtest0325a/PLC_1510SP1_PN|0/2/1
SETHWADDRESS|agenthwtest0325a/PLC_1510SP1_PN|0/2/1|Input|8
GETHWADDRESSES|agenthwtest0325a/PLC_1510SP1_PN|0/3/1
SETHWADDRESS|agenthwtest0325a/PLC_1510SP1_PN|0/3/1|Output|8
```

### Inspect and change drive telegrams
```text
GETDEVICEITEMS|dano/Drive_U50_1
GETDRIVEOBJECTS|dano/Drive_U50_1
GETDRIVETELEGRAMS|dano/Drive_U50_1|C18-VASP1|1
SETDRIVETELEGRAMNUMBER|dano/Drive_U50_1|C18-VASP1|MainTelegram|352|1
SETDRIVETELEGRAMNUMBER|dano/Drive_U50_1|C18-VASP1|SafetyTelegram|30|1
SETDRIVETELEGRAMADDRESS|dano/Drive_U50_1|C18-VASP1|MainTelegram|Input|1000|1
SETDRIVETELEGRAMADDRESS|dano/Drive_U50_1|C18-VASP1|MainTelegram|Output|1000|1
SETDRIVETELEGRAMADDRESS|dano/Drive_U50_1|C18-VASP1|SafetyTelegram|Input|2000|1
SETDRIVETELEGRAMADDRESS|dano/Drive_U50_1|C18-VASP1|SafetyTelegram|Output|2000|1
```

Notes:
- `GETDRIVEOBJECTS` summarizes the drive-capable device items and the drive object numbers discovered through the MC Drives service.
- `GETDRIVETELEGRAMS` exposes the current telegram number and telegram IO addresses for the selected drive object.
- `SETDRIVETELEGRAMNUMBER` can change an existing telegram number and can also insert missing `MainTelegram` or `SafetyTelegram` entries when Openness allows it.
- `SETDRIVETELEGRAMADDRESS` targets the IO address objects exposed by the selected telegram, which is the bridge equivalent of the earlier direct PowerShell drive experiments.

### Work with PLC tags
```text
GETPLCTAGTABLES|PackagingMachine/S7-1500/ET200MP station_1
GETPLCTAGS|PackagingMachine/S7-1500/ET200MP station_1
GETPLCTAGXREF|PackagingMachine/S7-1500/ET200MP station_1|02_Global/Global|MyTag|AllObjects
ADDPLCTAG|PackagingMachine/S7-1500/ET200MP station_1|02_Global/Global|MyTag|Bool|%M100.0
```

### Work with Unified HMI tags, connections, screens, and items
```text
GETHMITAGTABLES|PackagingMachine/HMI DualBag
GETHMITAGS|PackagingMachine/HMI DualBag
GETHMITAGXREF|PackagingMachine/HMI DualBag|Default tag table|HmiSpeed|AllObjects
ADDHMITAG|PackagingMachine/HMI DualBag|Default tag table|HmiSpeed|Int|DB10.DBW0|PLC_Connection
ENSUREHMITAG|PackagingMachine/HMI DualBag|Default tag table|HmiSpeed|Int|DB10.DBW0|PLC_Connection
ENSUREHMITAG|PackagingMachine/HMI DualBag|Default tag table|HmiInternalText|WString|-|-
UPDATEHMITAG|PackagingMachine/HMI DualBag|Default tag table|HmiSpeed|Int|DB10.DBW2|PLC_Connection
DELETEHMITAG|PackagingMachine/HMI DualBag|Default tag table|HmiSpeed
GETHMICONNECTIONS|PackagingMachine/HMI DualBag
GETHMICONNECTIONPROPERTIES|PackagingMachine/HMI DualBag|PLC_Connection
ADDHMICONNECTION|PackagingMachine/HMI DualBag|PLC_Connection|SIMATIC S7 1200/1500|-|false|Created by bridge
SETHMICONNECTIONPROPERTY|PackagingMachine/HMI DualBag|PLC_Connection|Comment|Updated by bridge
GETHMISCREENGROUPS|PackagingMachine/HMI DualBag
CREATEHMISCREENGROUP|PackagingMachine/HMI DualBag|<root>|Config
GETHMISCREENS|PackagingMachine/HMI DualBag
CREATEHMISCREEN|PackagingMachine/HMI DualBag|Config|Overview
GETHMISCREENPROPERTIES|PackagingMachine/HMI DualBag|Config/Overview
SETHMISCREENPROPERTY|PackagingMachine/HMI DualBag|Config/Overview|BackColor|#F5F5F5
GETHMISCREENITEMS|PackagingMachine/HMI DualBag|Config/Overview
GETHMISCREENITEMPROPERTIES|PackagingMachine/HMI DualBag|Config/Overview|Lbl_Title
GETHMISCREENITEMTAGBINDINGS|PackagingMachine/HMI DualBag|Config/Overview|Io_Speed
ADDHMISCREENITEM|PackagingMachine/HMI DualBag|Config/Overview|TEXT|Txt_Title|20|20|220|40|Device configuration
ADDHMISCREENITEM|PackagingMachine/HMI DualBag|Config/Overview|LINE|Ln_Separator|20|70|400|2
ADDHMISCREENITEM|PackagingMachine/HMI DualBag|Config/Overview|TEXTBOX|Txt_Notes|20|90|260|80|Operator note
ADDHMISCREENITEM|PackagingMachine/HMI DualBag|Config/Overview|GRAPHICVIEW|Gv_Logo|450|20|120|120
ENSUREHMISCREENITEM|PackagingMachine/HMI DualBag|Config/Overview|BUTTON|Btn_Ok|520|50|180|70|OK
SETHMISCREENITEMPROPERTY|PackagingMachine/HMI DualBag|Config/Overview|Txt_Title|Visible|true
SETHMISCREENITEMPROPERTY|PackagingMachine/HMI DualBag|Config/Overview|Txt_Title|Text|Device configuration
SETHMISCREENITEMPROPERTY|PackagingMachine/HMI DualBag|Config/Overview|Txt_Title|ForeColor|#003366
SETHMISCREENITEMPROPERTY|PackagingMachine/HMI DualBag|Config/Overview|Gv_Logo|Graphic|ProjectLogo
SETHMISCREENITEMTAGBINDING|PackagingMachine/HMI DualBag|Config/Overview|Io_Speed|ProcessValue|HmiSpeed
ENSUREHMISCREENITEMTAGBINDING|PackagingMachine/HMI DualBag|Config/Overview|Io_Speed|ProcessValue|HmiSpeed
SETHMISCREENITEMTAGBINDING|PackagingMachine/HMI DualBag|Config/Overview|Txt_Title|Visible|HmiShowTitle
```

Supported `ADDHMISCREENITEM` / `ENSUREHMISCREENITEM` types currently include:
- `LABEL`
- `BUTTON`
- `IOFIELD`
- `RECTANGLE`
- `TEXT`
- `LINE`
- `ELLIPSE`
- `TEXTBOX`
- `SYMBOLICIOFIELD`
- `GRAPHICVIEW`

Notes:
- `ENSUREHMITAG` makes HMI tag retries safer by creating the tag if missing and otherwise updating the existing tag in place, including internal tags created with `-|-` for address/connection.
- `ENSUREHMISCREENITEM` makes HMI layout retries safer by creating the item if missing and otherwise updating the existing item in place.
- `ENSUREHMISCREENITEMTAGBINDING` makes HMI binding retries safer by returning `unchanged` when the requested tag binding already exists and otherwise updating the binding in place.
- `GETPLCTAGXREF`, `GETPLCBLOCKXREF`, and `GETHMITAGXREF` expose Openness cross references for individual PLC tags, PLC blocks, and Unified HMI tags. Optional filter values are `AllObjects`, `ObjectsWithReferences`, `ObjectsWithoutReferences`, and `UnusedObjects`.
- For Unified multilingual text properties such as `Text`, plain text like `OK` is now normalized automatically to valid Unified markup, so XML-like text wrappers are no longer required for common cases.

### Inspect HMI ethernet parameters and other hardware properties
```text
GETHWPROPERTIES|PackagingMachine/HMI DualBag|DEVICE
SETHWPROPERTY|PackagingMachine/HMI DualBag|DEVICE|Comment|Unified HMI config by bridge
GETDEVICEITEMS|PackagingMachine/HMI DualBag
```

### Explore PLC blocks first
```text
GETPLCBLOCKGROUPS|PackagingMachine/S7-1500/ET200MP station_1
GETPLCBLOCKS|PackagingMachine/S7-1500/ET200MP station_1
GETPLCBLOCKSJSON|PackagingMachine/S7-1500/ET200MP station_1
GETPLCBLOCKXREF|PackagingMachine/S7-1500/ET200MP station_1|02_Global/Data|AllObjects
GETPLCBLOCKINFO|PackagingMachine/S7-1500/ET200MP station_1|02_Global/Data
GETPLCBLOCKINFOJSON|PackagingMachine/S7-1500/ET200MP station_1|02_Global/Data
```

### Export with smart strategy
```text
EXPORTPLCBLOCKSMART|PackagingMachine/S7-1500/ET200MP station_1|02_Global/Data|C:\Exports
EXPORTPLCBLOCKSMART|PackagingMachine/S7-1500/ET200MP station_1|04_ObjectsOfProjectLibraryUsed/ClearAlarms|C:\Exports
EXPORTPLCBLOCKSMARTJSON|PackagingMachine/S7-1500/ET200MP station_1|02_Global/Data|C:\Exports
```

### Import with smart strategy
```text
IMPORTPLCBLOCKSMART|PackagingMachine/S7-1500/ET200MP station_1|C:\Exports\Data_docs\Data.s7dcl|02_Global
IMPORTPLCBLOCKSMART|PackagingMachine/S7-1500/ET200MP station_1|C:\Exports\ClearAlarms.xml|04_ObjectsOfProjectLibraryUsed
IMPORTPLCBLOCKSMARTJSON|PackagingMachine/S7-1500/ET200MP station_1|C:\Exports\Data_docs\Data.s7dcl|02_Global
```

### Create a disposable test FB
```text
CREATEFB|PackagingMachine/S7-1500/ET200MP station_1|02_Global|Fb_AgentTest
COMPILEPLCBLOCK|PackagingMachine/S7-1500/ET200MP station_1|02_Global/Fb_AgentTest
```

### Create a disposable test FC
```text
CREATEFC|PackagingMachine/S7-1500/ET200MP station_1|02_Global|Fc_AgentTest
COMPILEPLCBLOCK|PackagingMachine/S7-1500/ET200MP station_1|02_Global/Fc_AgentTest
```

### Create a disposable test DB
```text
CREATEDB|PackagingMachine/S7-1500/ET200MP station_1|02_Global|Db_AgentTest
GETPLCBLOCKINFO|PackagingMachine/S7-1500/ET200MP station_1|02_Global/Db_AgentTest
COMPILEPLCBLOCK|PackagingMachine/S7-1500/ET200MP station_1|02_Global/Db_AgentTest
```

### Delete a disposable or imported test block
```text
DELETEPLCBLOCK|PackagingMachine/S7-1500/ET200MP station_1|02_Global/Fb_AgentTest
```

### Rename a disposable or imported test block
```text
RENAMEPLCBLOCK|PackagingMachine/S7-1500/ET200MP station_1|02_Global/Data_ImportedTest|Data_RenamedTest
```

---

## Files generated during session experiments

Examples of generated files used during testing:
- `exports-test/`
- `exports-kinds/`
- `exports-smart/`
- `imports-test/`

These are useful reference examples for understanding how TIA exports different block types.

---

## Notes and limitations

- Some SCL blocks may export correctly to XML but fail in document export even when `KnowHowProtected=False` is reported.
- Imported blocks may require compile/update actions afterward.
- `CREATEFB` now creates a disposable SCL FB through XML template import because direct Openness `CreateFB(...)` was observed to allow only `ProDiag` in this environment.
- `CREATEFC` now creates a disposable SCL FC through XML template import so FC creation follows the same import-style workflow as `CREATEFB`.
- `CREATEDB` creates a disposable global DB through minimal XML template import so DB creation follows the same import-style workflow as the other block creation commands.
- `CREATEFB`, `CREATEFC`, and `CREATEDB` now use cleaner minimal templates to keep generated disposable blocks small and easier to inspect.
- `GETDEVICEITEMS` now returns stable device item references based on plug-position paths such as `1`, `1/4`, `0/2`, or `0/2/1`; these references are intended for `GETPLUGLOCATIONS`, `ADDMODULE`, `GETHWPROPERTIES`, `SETHWPROPERTY`, `GETHWADDRESSES`, and `SETHWADDRESS`.
- `ADDMODULE` currently relies on Openness `PlugNew(...)` at a specific parent target and position.
- In the ET200SP live test, parent target `0` (rack) and positions `2` / `3` worked for inserting DI/DQ modules.
- `GETPLUGLOCATIONS` exists, but on the tested ET200SP rack it behaved unreliably and should not be the first diagnostic step there.
- `GETHWPROPERTIES` / `SETHWPROPERTY` work with Openness engineering attributes, so the exact property names, supported types, and write availability depend on the selected hardware object and TIA version.
- `GETHWADDRESSES` / `SETHWADDRESS` work with Openness `Address` objects and map to the TIA Portal I/O addresses page.
- In the ET200SP live test, module addresses were exposed on the module subslot (`0/2/1`, `0/3/1`) rather than on the module root (`0/2`, `0/3`).
- Not every TIA hardware parameter is necessarily exposed as a writable Openness attribute, especially for specialized modules or technology objects.
- `DELETEPLCBLOCK` should preferably be used on imported or disposable test blocks unless the user explicitly intends to remove an existing project block.
- `RENAMEPLCBLOCK` should preferably be used on imported or disposable test blocks unless the user explicitly intends to rename an existing project block.
- Block document import currently assumes a directory contains exactly one `.s7dcl` if a folder path is used.
- System blocks and library-related blocks may behave differently from user blocks.
- Smart import/export is based on current observed behavior and can be refined further with more project samples.

---

## To-do list

- [ ] Add remaining block creation commands (`CREATEINSTANCEDB`)
- [ ] Add `REMOVEMODULE` / `MOVEMODULE` style hardware commands
- [ ] Add JSON output variants for hardware item, slot, property, and address inspection
- [ ] Test hardware insertion workflows live with ET200, SM, and SIWAREX modules and document the required parent references and slot numbers
- [ ] Add direct address commands for additional address operations beyond start address if needed
- [ ] Investigate whether additional hardware parameters are accessible through services beyond generic engineering attributes and generic `Address` objects

- [ ] Add safer overwrite / dry-run options for import
- [ ] Detect and report compile-required state explicitly after import
- [ ] Investigate why some SCL blocks fail document export with a know-how-protected error even when block info reports otherwise
- [ ] Test import behavior for LAD document exports (`.s7dcl` + `.s7res`)
- [ ] Test import behavior for SCL XML exports after manual code edits
- [ ] Test whether XML import can update existing code blocks cleanly without manual cleanup
- [ ] Add better handling for multilingual resource files during document import/export
- [ ] Add command output length management or JSON paging for large block/tag lists
- [ ] Add automated regression test scripts for example projects

---

## Summary

During this session the bridge evolved from simple project/device discovery into a more capable TIA automation tool with:
- robust device reference handling
- PLC module discovery with reusable device item references
- direct hardware module insertion through `ADDMODULE`
- direct hardware property inspection and editing through `GETHWPROPERTIES` and `SETHWPROPERTY`
- direct hardware address discovery and editing through `GETHWADDRESSES` and `SETHWADDRESS`
- direct drive telegram discovery and editing through `GETDRIVEOBJECTS`, `GETDRIVETELEGRAMS`, `SETDRIVETELEGRAMNUMBER`, and `SETDRIVETELEGRAMADDRESS`
- live-verified ET200SP DI/DQ module insertion and I/O start-address changes
- direct PLC tag editing
- PLC block discovery
- block export in multiple formats
- smart export selection by block type/language
- smart block import tested successfully on a renamed DB block
- template-based disposable block creation through `CREATEFB`, `CREATEFC`, and `CREATEDB`, with cleaner minimal templates

The next major step should be live-testing the same workflow on additional hardware families such as SIWAREX and refining specialized commands where generic attributes or generic address objects are not enough.
