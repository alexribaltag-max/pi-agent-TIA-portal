import assert from 'node:assert/strict';

// For captured tool responses; does not launch a bridge or contact TIA.
export function assertLegacySuccess(response, command, resultType = 'text') {
  assert.equal(response.type, 'response', 'Fatal/event is not a command response');
  assert.equal(response.command, command, 'Response belongs to another command');
  assert.equal(response.status, 'success', response.error || 'Bridge reported failure');
  assert.equal(response.resultType, resultType);
  if (resultType === 'text') {
    assert.equal(typeof response.result, 'string');
    assert.ok(response.result.trim().length, 'Empty text success');
  } else {
    assert.ok(response.result && typeof response.result === 'object', 'Missing JSON result');
  }
  return response.result;
}

// Phase 0 contract invariants, not a complete JSON schema or production renderer.
export function assertBlockReadResult(result) {
  assert.equal(result.schemaVersion, 1);
  assert.equal(result.analysisOnly, true);
  for (const name of ['project', 'deviceRef', 'blockRef', 'blockType', 'language', 'view', 'representation']) {
    assert.ok(typeof result[name] === 'string' && result[name].length, `Missing ${name}`);
  }
  assert.ok(['native-text', 'reconstructed', 'partial', 'metadata-only'].includes(result.fidelity));
  assert.equal(typeof result.completeForSelection, 'boolean');
  assert.ok(Array.isArray(result.unsupportedConstructs));
  assert.ok(Array.isArray(result.warnings));
  assert.equal(typeof result.content, 'string');
  assert.ok(Buffer.byteLength(result.content, 'utf8') <= 16384, 'Default content byte budget');
  const lines = result.content.length ? result.content.split(/\r\n|\r|\n/).length : 0;
  assert.ok(lines <= 300, 'Default content line budget');
  assert.equal(typeof result.page.hasMore, 'boolean');
  if (result.page.hasMore) assert.ok(typeof result.page.nextCursor === 'string' && result.page.nextCursor.length);
  else assert.equal(result.page.nextCursor, null);
  assert.ok(typeof result.snapshot.artifactId === 'string' && result.snapshot.artifactId.length);
  assert.match(result.snapshot.hash, /^[a-f0-9]{64}$/);
  assert.ok(Number.isFinite(Date.parse(result.snapshot.capturedAt)));
  assert.ok(['fresh-export', 'immutable-snapshot', 'unverified'].includes(result.snapshot.freshness));
  if (['partial', 'metadata-only'].includes(result.fidelity)) {
    assert.equal(result.completeForSelection, false);
    assert.ok(result.warnings.length, 'Incomplete conversion must be visible');
  }
  if (result.unsupportedConstructs.length) {
    assert.equal(result.fidelity, 'partial');
    assert.equal(result.completeForSelection, false);
  }
  if (result.fidelity === 'metadata-only') {
    assert.ok(['PROTECTED', 'EXPORT_UNSUPPORTED', 'FORMAT_UNSUPPORTED', 'EXPORT_FAILED'].includes(result.unavailableReason));
    assert.equal(result.content, '');
  }
}
