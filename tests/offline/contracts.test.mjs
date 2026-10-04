import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { encodeDeviceReference, decodeDeviceReference } from '../support/device-reference.mjs';
import { assertLegacySuccess, assertBlockReadResult } from '../support/assert-results.mjs';

const vectors = JSON.parse(readFileSync(new URL('../contracts/device-references.json', import.meta.url), 'utf8'));

test('canonical reference vectors round-trip without legacy delimiters or normalization', () => {
  const references = new Set();
  for (const { parts, legacy } of vectors.valid) {
    const reference = encodeDeviceReference(parts);
    assert.deepEqual(decodeDeviceReference(reference), parts);
    assert.doesNotMatch(reference, /[|\r\n\t/]/);
    references.add(reference);
    if (legacy) assert.equal(legacy, `${parts[0]}/${parts[3]}`);
  }
  assert.equal(references.size, vectors.valid.length, 'Groups/Unicode must not collapse');
  assert.equal(encodeDeviceReference(['P', 'root', [], 'D']), 'tia-device:v1:WyJQIiwicm9vdCIsW10sIkQiXQ');
});

test('invalid components, encodings and versions fail closed', () => {
  for (const parts of vectors.invalidParts) assert.throws(() => encodeDeviceReference(parts));
  assert.throws(() => encodeDeviceReference(['P', 'root', [], '\ud800']));
  const reference = encodeDeviceReference(vectors.valid[0].parts);
  for (const bad of ['', 'P/D', reference.replace(':v1:', ':v2:'), `${reference}=`, `${reference}|`, 'tia-device:v1:_w']) {
    assert.throws(() => decodeDeviceReference(bad), bad);
  }
  const spaced = Buffer.from(JSON.stringify(vectors.valid[0].parts, null, 2)).toString('base64url');
  assert.throws(() => decodeDeviceReference(`tia-device:v1:${spaced}`));
});

const response = { type: 'response', command: 'GETDEVICES', status: 'success', resultType: 'text', result: 'Project devices: PLC' };
test('legacy assertions reject bridge errors, fatal events and mismatched commands', () => {
  assert.equal(assertLegacySuccess(response, 'GETDEVICES'), response.result);
  for (const patch of [{ status: 'error', error: 'Not found' }, { type: 'fatal' }, { type: 'event' }, { command: 'LIST' }, { result: '' }]) {
    assert.throws(() => assertLegacySuccess({ ...response, ...patch }, 'GETDEVICES'));
  }
});

test('GETDEVICESJSON baseline is a single-device result, not an inventory', () => {
  const singleDevice = { project: 'P', device: { name: 'D', reference: 'P/D', hasPlcSoftware: true } };
  const result = assertLegacySuccess({ ...response, command: 'GETDEVICESJSON', resultType: 'json', result: singleDevice }, 'GETDEVICESJSON', 'json');
  assert.equal(result.device.reference, 'P/D');
  assert.equal(Array.isArray(result), false);
});

function blockResult() {
  return {
    schemaVersion: 1, analysisOnly: true, project: 'P', deviceRef: encodeDeviceReference(['P', 'root', [], 'D']),
    blockRef: 'Blocks/Main', blockType: 'FC', language: 'SCL', view: 'compact', representation: 'structured-text-v1',
    fidelity: 'reconstructed', completeForSelection: true, unsupportedConstructs: [], warnings: [], content: '#Ready := TRUE;',
    page: { hasMore: false, nextCursor: null, returnedRange: 'unit:1' },
    snapshot: { artifactId: 'synthetic-contract-example', hash: 'a'.repeat(64), capturedAt: '2026-01-01T00:00:00Z', freshness: 'fresh-export' },
  };
}

test('pagination and parser fidelity are independent', () => {
  const result = blockResult();
  assertBlockReadResult(result);
  result.page = { hasMore: true, nextCursor: 'opaque-test-cursor', returnedRange: 'unit:1' };
  assertBlockReadResult(result); // complete conversion of selection, more delivery pages
  result.fidelity = 'partial';
  result.completeForSelection = false;
  result.unsupportedConstructs = ['SyntheticUnknownInstruction'];
  result.warnings = ['Unsupported instruction retained in native artifact; logic incomplete.'];
  assertBlockReadResult(result);
  result.completeForSelection = true;
  assert.throws(() => assertBlockReadResult(result));
});

test('protected metadata cannot appear as empty complete logic', () => {
  const result = blockResult();
  Object.assign(result, { fidelity: 'metadata-only', content: '', completeForSelection: false, unavailableReason: 'PROTECTED', warnings: ['Requested logic unavailable.'] });
  assertBlockReadResult(result);
  delete result.unavailableReason;
  assert.throws(() => assertBlockReadResult(result));
});

test('default budgets count UTF-8 bytes and reject missing continuation/warnings', () => {
  for (const content of ['é'.repeat(8193), 'x\n'.repeat(300)]) {
    assert.throws(() => assertBlockReadResult({ ...blockResult(), content }));
  }
  assert.throws(() => assertBlockReadResult({ ...blockResult(), analysisOnly: false }));
  assert.throws(() => assertBlockReadResult({ ...blockResult(), page: { hasMore: true, nextCursor: null } }));
  assert.throws(() => assertBlockReadResult({ ...blockResult(), fidelity: 'partial', completeForSelection: false }));
});
