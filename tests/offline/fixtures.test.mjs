import test from 'node:test';
import assert from 'node:assert/strict';
import { manifest, fixtureBytes, canonicalBytes, sha256 } from '../support/fixtures.mjs';

test('baseline labels missing coverage and does not claim V21 validation', () => {
  assert.equal(manifest.schemaVersion, 1);
  assert.ok(manifest.missingCoverage.includes('V21 runtime exports'));
  assert.equal(manifest.fixtures.length, 4);
});

for (const fixture of manifest.fixtures) {
  test(`${fixture.id}: reviewed bytes and metadata are unchanged`, () => {
    assert.deepEqual(fixture.files.map(file => file.path), [fixture.xml, ...fixture.documents]);
    for (const file of fixture.files) {
      const bytes = canonicalBytes(fixtureBytes(file.path));
      assert.equal(bytes.length, file.canonicalBytes, file.path);
      assert.equal(sha256(bytes), file.canonicalSha256, file.path);
    }
    // Signature checks on trusted fixtures, NOT an XML or PLC parser.
    const xml = fixtureBytes(fixture.xml).toString('utf8');
    assert.ok(xml.includes(`<Engineering version="${fixture.tiaVersion}"`));
    assert.ok(xml.includes(`<SW.Blocks.${fixture.blockType} ID=`));
    assert.ok(xml.includes(`<ProgrammingLanguage>${fixture.language}</ProgrammingLanguage>`));
  });
}

test('LAD seed includes native networks and the referenced resources', () => {
  const fixture = manifest.fixtures.find(f => f.id === 'lad-ob');
  const document = fixtureBytes(fixture.documents[0]).toString('utf8');
  const resources = fixtureBytes(fixture.documents[1]).toString('utf8');
  assert.equal((document.match(/^\s*NETWORK\s*$/gm) || []).length, 6);
  assert.ok(document.includes('Contact( #Initial_Call )'));
  assert.ok(document.includes('Coil( "Data".general.firstCycle )'));
  const keys = [...document.matchAll(/S7_Network(?:Title|Comment) := "([^"]+)"/g)].map(m => m[1]);
  assert.equal(keys.length, 10);
  for (const key of keys) assert.ok(resources.includes(`id: ${key}`), `Missing ${key}`);
  assert.ok(resources.includes('en-US: First Cycle'));
});

test('canonical fixture hashes normalize checkout EOLs but retain BOM', () => {
  assert.deepEqual(canonicalBytes(Buffer.from('\ufeffA\r\nB')), Buffer.from('\ufeffA\nB'));
  assert.throws(() => canonicalBytes(Buffer.from([0xff])));
  assert.throws(() => fixtureBytes('../data.txt'));
});
