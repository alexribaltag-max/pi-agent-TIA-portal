// Reads installed XML reference documentation only. No DLL loads or TIA connection.
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { createHash } from 'node:crypto';

const directory = process.argv[2] || 'C:/Program Files/Siemens/Automation/Portal V21/PublicAPI/V21/net48';
const surfaces = {
  'Siemens.Engineering.Base.xml': [
    'P:Siemens.Engineering.ProjectBase.Devices',
    'P:Siemens.Engineering.ProjectBase.DeviceGroups',
    'P:Siemens.Engineering.ProjectBase.UngroupedDevicesGroup',
    'P:Siemens.Engineering.HW.DeviceGroup.Devices',
    'P:Siemens.Engineering.HW.DeviceUserGroup.Groups',
  ],
  'Siemens.Engineering.Step7.xml': [
    'M:Siemens.Engineering.SW.Blocks.PlcBlock.ExportAsDocuments(System.IO.DirectoryInfo,System.String)',
    'M:Siemens.Engineering.SW.ExternalSources.PlcExternalSourceSystemGroup.GenerateSource(System.Collections.Generic.IEnumerable{Siemens.Engineering.SW.ExternalSources.IGenerateSource},System.IO.FileInfo)',
    'M:Siemens.Engineering.SW.ExternalSources.PlcExternalSourceSystemGroup.GenerateSource(System.Collections.Generic.IEnumerable{Siemens.Engineering.SW.ExternalSources.IGenerateSource},System.IO.FileInfo,Siemens.Engineering.SW.ExternalSources.GenerateOptions)',
  ],
};
const files = Object.entries(surfaces).map(([file, members]) => {
  const bytes = readFileSync(resolve(directory, file));
  const text = bytes.toString('utf8');
  return {
    file, sha256: createHash('sha256').update(bytes).digest('hex'),
    members: members.map(member => ({ member, documented: text.includes(`<member name="${member}">`) })),
  };
});
const allDocumented = files.every(file => file.members.every(member => member.documented));
console.log(JSON.stringify({
  schemaVersion: 1, evidenceKind: 'installed-api-documentation-only', directory,
  allDocumented, runtimeSupport: 'unverified', projectUnchanged: 'not-tested',
  nativeSourcePolicy: 'disabled-until-disposable-runtime-verification', files,
}, null, 2));
if (!allDocumented) process.exitCode = 1;
