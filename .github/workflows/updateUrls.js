import fs from 'node:fs/promises';

const versionJson = await readJson('../versions/updates.json');

const assetJson = JSON.parse(process.env['RELEASE_ASSETS']);
for (const asset of assetJson) {
    const m = asset.name.match(/^([^-]+)-(.+)\.tgz$/);
    if (!m) throw new Error(`Asset name didn't match pattern: ${asset.name}`);
    const name = m[1];
    const version = m[2];
    const entry = versionJson.packages.find(v => v.id === name && v.latestVersion === version);
    if (!entry) throw new Error(`Asset didn't match an entry in the version manifest: ${name} ${version}`);
    entry.latestUpmTargz = asset.browser_download_url;
}

await writeJson('../versions/updates.json', versionJson);

async function readJson(file) {
    return JSON.parse(await fs.readFile(file, {encoding: 'utf-8'}));
}
async function writeJson(file, obj) {
    await fs.writeFile(file, JSON.stringify(obj, null, 2));
}
