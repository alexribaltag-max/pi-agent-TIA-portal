// Run after building tests/dotnet/Inventory.Tests.csproj. This executes only the
// pure test program; it never launches TiaLocalBridge or loads Siemens assemblies.
import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { encodeDeviceReference, decodeDeviceReference } from '../support/device-reference.mjs';

const vectorsPath = fileURLToPath(new URL('../contracts/device-references.json', import.meta.url));
const assembly = fileURLToPath(new URL('../dotnet/bin/Debug/net8.0/Inventory.Tests.dll', import.meta.url));
const vectors = JSON.parse(readFileSync(vectorsPath, 'utf8')).valid;
const lines = execFileSync('dotnet', [assembly, '--encode-vectors', vectorsPath], { encoding: 'utf8' }).trim().split(/\r?\n/);
assert.equal(lines.length, vectors.length);
for (let index = 0; index < vectors.length; index++) {
  assert.equal(lines[index], encodeDeviceReference(vectors[index].parts));
  assert.deepEqual(decodeDeviceReference(lines[index]), vectors[index].parts);
}
console.log(`PASS: ${vectors.length} production C# reference encodings exactly match the Phase 0 JavaScript oracle.`);
