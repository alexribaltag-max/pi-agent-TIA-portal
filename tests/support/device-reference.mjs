// Executable contract oracle ONLY. Phase 1 must test its C# codec against the same vectors.
const prefix = 'tia-device:v1:';
const utf8 = new TextDecoder('utf-8', { fatal: true });

function validate(parts) {
  if (!Array.isArray(parts) || parts.length !== 4) throw new Error('Invalid reference tuple');
  const [project, kind, groups, device] = parts;
  const validName = value => typeof value === 'string' && value.length > 0 && value.isWellFormed();
  if (!validName(project) || !validName(device) ||
      !['root', 'ungrouped', 'userGroup'].includes(kind) ||
      !Array.isArray(groups) || !groups.every(validName) ||
      (kind === 'userGroup' ? groups.length === 0 : groups.length !== 0)) {
    throw new Error('Invalid reference components');
  }
  return parts;
}

export function encodeDeviceReference(parts) {
  return prefix + Buffer.from(JSON.stringify(validate(parts)), 'utf8').toString('base64url');
}

export function decodeDeviceReference(reference) {
  if (typeof reference !== 'string' || !reference.startsWith(prefix)) throw new Error('Unsupported reference version');
  const encoded = reference.slice(prefix.length);
  if (!/^[A-Za-z0-9_-]+$/.test(encoded)) throw new Error('Invalid base64url');
  const parts = validate(JSON.parse(utf8.decode(Buffer.from(encoded, 'base64url'))));
  if (encodeDeviceReference(parts) !== reference) throw new Error('Noncanonical reference encoding');
  return parts;
}
