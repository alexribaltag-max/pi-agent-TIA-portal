# Offline baseline, contract, and inventory tests

These suites never start the bridge, attach to TIA, load Siemens DLLs, or edit project content. Node **22.23.3** is the pinned runtime for Phase 0 checks; Phase 1 adds .NET SDK **8.0.425** pure production-service tests. No npm or NuGet packages are required.

From the repository root:

```text
npm --prefix tests test
npm --prefix tests run baseline
```

Or without npm:

```text
node --test tests/offline/*.test.mjs
node tests/scripts/fixture-baseline.mjs
```

The optional **installed-documentation** probe requires the V21 API XML files, not a licensed/running TIA instance:

```text
node tests/scripts/probe-v21-api.mjs
node tests/scripts/probe-v21-api.mjs "D:/Siemens/PublicAPI/V21/net48"
```

Missing files/members fail the probe. Presence proves only documented API availability, not runtime block support, wrapper equality, read-only behavior, or licensing. It does not execute `GenerateSource`.

## Coverage and boundaries

- Four existing V20 sample sets / eight files are referenced in place by `fixtures/manifest.json`; no local machine exports were copied.
- SHA-256 and byte counts detect fixture drift. Canonical fixture hashes normalize CRLF to LF while retaining UTF-8 BOM. Native export/artifact hashes must instead use exact raw bytes.
- Trusted-fixture metadata/resource signature checks are **not** XML/SCL/YAML parsers or semantic-equivalence tests.
- Canonical reference vectors cover slash-containing names, nested group segmentation, reserved characters, Unicode, control characters, malformed encoding, and version rejection. `support/device-reference.mjs` remains an executable contract oracle; the separate Phase 1 suite now tests the production C# codec and resolver.
- Synthetic result tests enforce failure handling, partial/protected content notices, pagination separation, and byte/line ceilings. They do not certify the current bridge. `assertLegacySuccess` can also validate captured bridge response objects in later integration tests.
- Baseline output compares **artifact bytes**, including resources. It makes no token-compression, parser-fidelity, or latency claim.
- `.github/workflows/offline-tests.yml` runs this suite on Windows and Linux without Siemens software. A local Windows pass is not a claim that hosted CI has already run.

Do not regenerate the fixture manifest merely to make a failed test pass. Review provenance, version labels, and exact diffs first. Do not commit customer PLC code or publish copied Siemens samples without permission/license review.

## Phase 1 — actual production C# inventory tests

The test projects link the production pure codec, inventory, resolver, options, JSON and paging source files. The Siemens adapter is covered by a separate bridge compile check, not mocked runtime acceptance.

From the repository root, enter `tests/` so `tests/global.json` selects the pinned SDK:

```text
cd tests
dotnet run --project dotnet/Inventory.Tests.csproj
node scripts/check-reference-interop.mjs
```

Expected: 33 C# tests pass, then seven C# reference encodings match the Phase 0 JavaScript oracle exactly. The interop script executes only the pure test assembly.

On Windows with the .NET Framework 4.8 Developer Pack, from `tests/`:

```powershell
dotnet msbuild net48/Inventory.Smoke.csproj -t:Build -verbosity:minimal
.\net48\bin\Inventory.Smoke.exe
```

Expected: four net48 smoke checks pass, including block snapshot signing and Unicode continuation. This confirms the same framework serializer/codec/paging paths work without Siemens DLLs; it does not launch `TiaLocalBridge.exe`.

For an **isolated compile check only**, from the repository root on a V21 development machine:

```text
dotnet msbuild TiaLocalBridge/TiaLocalBridge.csproj -t:Rebuild -p:Configuration=Debug -p:OutputPath=bin/Phase1Offline/ -p:IntermediateOutputPath=obj/Phase1Offline/ -verbosity:minimal
```

This does not replace the normal Debug executable, start the bridge, or test a live project. Build outputs under `bin/` and `obj/` remain ignored. CI runs the pure tests on Windows/Linux and the net48 smoke tests on Windows; hosted CI execution is not claimed by the local report.

See [Phase 1 evidence and limitations](../docs/PHASE_1_DEVICE_INVENTORY.md), especially the conservative wrapper-identity policy and the distinction between inventory fingerprints and immutable snapshots.

## Phase 2 offline preview checks

Production-linked snapshot/document checks (no Siemens assemblies or TIA connection):

```text
cd tests
dotnet run --project block-snapshot/BlockSnapshot.Tests.csproj
```

Expected: 45 checks, including fail-closed export behavior, resource handling, signed snapshot continuation, hash/tamper checks, bounded Unicode slices and conservative paging. The old V20 fixture files are read in place, not modified; the test writes a temporary snapshot under the OS temp directory and removes it. The new `GETPLCBLOCK` command itself is only bridge-compiled, not executed live. See [Phase 2 limitations](../docs/PHASE_2_BLOCK_READER.md).

## Phase 3 interim SCL XML parser checks

```text
cd tests
dotnet run --project structured-text/StructuredText.Tests.csproj
```

Expected: 23 production-linked checks against the existing V20 SCL fixture, synthetic symbols/multiple units and rejected XML/schema/semantic variants; no Siemens assemblies or live TIA. See [the interim report](../docs/PHASE_3_SCL_INTERIM.md). Stage 3 and the older live gates are not complete.

## Live testing is a separate gate

Use the `tiabridge` tool for approved TIA operations. See [Phase 0 baseline and runtime checklist](../docs/PHASE_0_BASELINE.md). No live suite runs from `npm test` or CI.

The historical `TiaLocalBridge/scripts/test-plc-block-kind-exports.ps1` recursively deletes `ExportRoot` and ignores command status; it was **not run or modified** here. Do not point it at tracked fixture directories. The old scripts remain historical workflow references, not passing regression evidence.

Phase 1 now tests the actual C# pure inventory services without Siemens dependencies. Later phases should add production parser tests and mock-extension lifecycle tests as that code arrives. Do not substitute the Phase 0 JavaScript oracle for production implementation tests. Live tests are currently deferred by the user's explicit instruction.
