import fs from 'node:fs/promises';
import tar from 'tar';
import hasha from 'hasha';
import semver from 'semver';
import tmp from 'tmp-promise';

const versionJson = await readJson('../versions/updates.json');
await rmdir('dist');
await fs.mkdir('dist');

for (const dir of await fs.readdir('.')) {
    const packageJsonPath = `${dir}/package.json`
    if (!await checkFileExists(packageJsonPath)) {
        continue;
    }

    console.log(`Packaging ${dir}`);

    const json = await readJson(packageJsonPath);
    const name = json.name;

    let existing = versionJson.packages.find(e => e.id === name);
    if (existing) {
        json.version = existing.latestVersion;
        await writeJson(packageJsonPath, json);
        if ((await md5Dir(dir)) === existing.hash) {
            console.log("Hash already matches, skipping ...");
            continue;
        }
    }

    let version = '1.0.0';
    if (existing) {
        version = existing.latestVersion;
        version = semver.inc(version, 'minor');
    }
    json.version = version;
    await writeJson(packageJsonPath, json);

    const outputFilename = `dist/${name}-${version}.tgz`;
    await createTar(dir, outputFilename);

    if (!existing) {
        existing = { id: name };
        versionJson.packages.push(existing);
    }
    existing.latestVersion = version;
    existing.hash = await hasha.fromFile(outputFilename, {algorithm: 'sha256'});
    existing.displayName = json.displayName;
    console.log(`Adding to version repository with version ${version}`);
}

await writeJson('../versions/updates.json', versionJson);

function checkFileExists(file) {
    return fs.access(file, fs.constants.F_OK)
        .then(() => true)
        .catch(() => false)
}

async function md5Dir(dir) {
    const tmpFile = (await tmp.file()).path;
    await createTar(dir, tmpFile);
    const md5 = await hasha.fromFile(tmpFile, {algorithm: 'sha256'});
    await fs.unlink(tmpFile);
    return md5;
}
async function readJson(file) {
    return JSON.parse(await fs.readFile(file, {encoding: 'utf-8'}));
}
async function writeJson(file, obj) {
    await fs.writeFile(file, JSON.stringify(obj, null, 2));
}
async function rmdir(path) {
    if (await checkFileExists(path)) {
        await fs.rm(path, {recursive: true});
    }
}
async function createTar(dir, outputFilename) {
    await tar.create({
        gzip: true,
        cwd: dir,
        file: outputFilename,
        portable: true,
        noMtime: true,
        prefix: 'package/'
    }, await fs.readdir(dir));
}
