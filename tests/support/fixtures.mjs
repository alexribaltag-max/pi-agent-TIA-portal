import { readFileSync } from 'node:fs';
import { createHash } from 'node:crypto';

export const repoRoot = new URL('../../', import.meta.url);
export const manifest = JSON.parse(readFileSync(new URL('../fixtures/manifest.json', import.meta.url), 'utf8'));
export const sha256 = bytes => createHash('sha256').update(bytes).digest('hex');
export function fixtureBytes(path) {
  if (!path.startsWith('TiaLocalBridge/exports-kinds/') || path.includes('..') || path.includes('\\')) {
    throw new Error('Fixture outside reviewed allowlist');
  }
  return readFileSync(new URL(path, repoRoot));
}
// Git enforces CRLF for XML and auto-text files. Canonical fixture identity is LF
// UTF-8, retaining BOM and all other bytes. Native artifact hashes MUST use raw bytes.
export function canonicalBytes(bytes) {
  const text = new TextDecoder('utf-8', { fatal: true, ignoreBOM: true }).decode(bytes);
  return Buffer.from(text.replace(/\r\n/g, '\n'), 'utf8');
}
