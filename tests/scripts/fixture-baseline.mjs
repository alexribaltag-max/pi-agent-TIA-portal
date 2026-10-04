import { manifest, fixtureBytes, canonicalBytes, sha256 } from '../support/fixtures.mjs';

const rows = manifest.fixtures.map(fixture => {
  for (const file of fixture.files) {
    const bytes = canonicalBytes(fixtureBytes(file.path));
    if (sha256(bytes) !== file.canonicalSha256 || bytes.length !== file.canonicalBytes) {
      throw new Error(`Fixture changed; review before re-baselining: ${file.path}`);
    }
  }
  const xmlBytes = fixtureBytes(fixture.xml).length;
  const documentBytes = fixture.documents.length
    ? fixture.documents.reduce((sum, path) => sum + fixtureBytes(path).length, 0) : null;
  return {
    id: fixture.id, tiaVersion: fixture.tiaVersion, xmlBytes, documentBytes,
    artifactByteReductionPercent: documentBytes === null ? null : Number(((1 - documentBytes / xmlBytes) * 100).toFixed(1)),
  };
});
console.log(JSON.stringify({
  runtime: process.version,
  measurement: 'Raw filesystem bytes on this checkout; NOT tokens or verified equivalent semantic scope. Documents include resources.',
  parserFidelity: 'not tested', modelFacingTokens: null, liveLatency: null, rows,
}, null, 2));
