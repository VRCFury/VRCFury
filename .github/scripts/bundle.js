import process from 'node:process';
import fs from 'node:fs/promises';
import fsPlain from 'node:fs';
import path from 'node:path';
import {create as tarCreate} from 'tar';
import { hashFile } from 'hasha';
import semver from 'semver';
import tmp from 'tmp-promise';
import { spawn } from 'promisify-child-process';
import archiver from 'archiver';
import { once } from 'node:events';

const [vrcfuryRepoPathArg, versionManifestRepoPathArg] = process.argv.slice(2);
if (!vrcfuryRepoPathArg || !versionManifestRepoPathArg) {
    throw new Error('Usage: bundle.js <vrcfury-repo-path> <version-manifest-repo-path>');
}

const updatesJsonPath = path.join(versionManifestRepoPathArg, 'updates.json');
const vccJsonPath = path.join(versionManifestRepoPathArg, 'vcc.json');

await assertDirExists(vrcfuryRepoPathArg, 'VRCFury repo path');
await assertDirExists(versionManifestRepoPathArg, 'Version manifest repo path');

const versionJson = await readJson(updatesJsonPath);
const vccJson = await readJson(vccJsonPath);
const { path: distDir, cleanup: cleanupDistDir } = await tmp.dir({ unsafeCleanup: true, prefix: 'vrcfury-bundle-' });

try {
    const allTags = await getTags();

    for (const dir of await fs.readdir(vrcfuryRepoPathArg)) {
        const packageDirPath = path.join(vrcfuryRepoPathArg, dir);
        const packageJsonPath = path.join(packageDirPath, 'package.json');
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
            if ((await md5Dir(packageDirPath)) === existing.hash) {
                console.log("Hash already matches, skipping ...");
                continue;
            }
        }

        const tagPrefix = `${name}/`;
        let version = await getNextVersion(allTags, tagPrefix);
        const tagName = `${tagPrefix}${version}`
        json.version = version;
        await writeJson(packageJsonPath, json);

        const outputFilename = `${name}-${version}.tgz`;
        const outputPath = path.join(distDir, outputFilename);
        const outputUrl = `https://github.com/VRCFury/VRCFury/releases/download/${encodeURIComponent(tagName)}/${encodeURIComponent(outputFilename)}`;
        await createTar(packageDirPath, outputPath);

        if (!existing) {
            existing = { id: name };
            versionJson.packages.push(existing);
        }
        existing.latestVersion = version;
        existing.hash = await hashFile(outputPath, {algorithm: 'sha256'});
        existing.displayName = json.displayName;
        existing.latestUpmTargz = outputUrl;

        if (name === "com.vrcfury.installer") {
            const updater = versionJson.packages.find(e => e.id === "com.vrcfury.updater");
            if (updater) {
                updater.latestVersion = existing.latestVersion;
                updater.hash = existing.hash;
                updater.displayName = existing.displayName;
                updater.latestUpmTargz = existing.latestUpmTargz;
            }
        }
        console.log(`Adding to version repository with version ${version}`);

        const outputZipFilename = `${name}-${version}-vcc.zip`;
        const outputZipPath = path.join(distDir, outputZipFilename);
        const outputZipUrl = `https://github.com/VRCFury/VRCFury/releases/download/${encodeURIComponent(tagName)}/${encodeURIComponent(outputZipFilename)}`;
        await createZip(packageDirPath, outputZipPath);

        if (name === 'com.vrcfury.vrcfury') {
            let existingVcc = vccJson.packages[name];
            if (!existingVcc) {
                existingVcc = vccJson.packages[name] = {
                    versions: {}
                };
            }
            if (existingVcc.versions[version]) {
                throw new Error("Version already exists in vcc.json");
            }
            const vccPackage = JSON.parse(JSON.stringify(json));
            vccPackage.url = outputZipUrl;
            existingVcc.versions[version] = vccPackage;
        }

        await spawn('git', [ 'config', '--global', 'user.email', 'noreply@vrcfury.com' ], { stdio: "inherit", cwd: vrcfuryRepoPathArg });
        await spawn('git', [ 'config', '--global', 'user.name', 'VRCFury Releases' ], { stdio: "inherit", cwd: vrcfuryRepoPathArg });
        await spawn('git', [ 'commit', '-m', `${json.displayName} v${version}`, path.relative(vrcfuryRepoPathArg, packageJsonPath) ], { stdio: "inherit", cwd: vrcfuryRepoPathArg });
        await spawn('git', [ 'tag', tagName ], { stdio: "inherit", cwd: vrcfuryRepoPathArg });
        await spawn('git', [ 'push', 'origin', tagName ], { stdio: "inherit", cwd: vrcfuryRepoPathArg });
        await spawn('git', [ 'checkout', process.env.GITHUB_SHA ], { stdio: "inherit", cwd: vrcfuryRepoPathArg });

        await spawn('gh', [
            'release',
            'create',
            tagName,
            outputPath,
            outputZipPath,
            '--title', `${json.displayName} v${version}`,
            '--verify-tag'
        ], { stdio: "inherit", cwd: vrcfuryRepoPathArg });
    }
} finally {
    await cleanupDistDir();
}

await writeJson(updatesJsonPath, versionJson);

for (const p of Object.values(vccJson.packages)) {
    p.versions = Object.fromEntries(
        Object.entries(p.versions)
            .sort(([v1], [v2]) => semver.compare(v2, v1)),
    );
}
await writeJson(vccJsonPath, vccJson);

function checkFileExists(file) {
    return fs.access(file, fs.constants.F_OK)
        .then(() => true)
        .catch(() => false)
}

async function assertDirExists(dir, name) {
    let stat;
    try {
        stat = await fs.stat(dir);
    } catch {
        throw new Error(`${name} does not exist: ${dir}`);
    }
    if (!stat.isDirectory()) {
        throw new Error(`${name} is not a directory: ${dir}`);
    }
}

async function md5Dir(dir) {
    const tmpFile = (await tmp.file()).path;
    await createTar(dir, tmpFile);
    const md5 = await hashFile(tmpFile, {algorithm: 'sha256'});
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
    await tarCreate({
        gzip: true,
        cwd: dir,
        file: outputFilename,
        portable: true,
        noMtime: true,
        prefix: 'package/'
    }, await fs.readdir(dir));
}
async function createZip(dir, outputFilename) {
    const output = fsPlain.createWriteStream(outputFilename);
    const archive = archiver('zip', {
        zlib: { level: 9 } // Sets the compression level.
    });
    const streamClosed = Promise.race([
        once(output, 'close'),
        once(output, 'error').then(([error]) => Promise.reject(error)),
        once(archive, 'error').then(([error]) => Promise.reject(error))
    ]);
    archive.pipe(output);
    archive.directory(dir, false);
    await archive.finalize();
    await streamClosed;
}

async function getTags() {
    const { stdout, stderr } = await spawn('git', ['ls-remote', '--tags', 'origin'], {
        encoding: 'utf8',
        cwd: vrcfuryRepoPathArg
    });
    return (stdout+'')
        .split('\n')
        .filter(line => line.includes("refs/tags/"))
        .map(line => line.substring(line.indexOf('refs/tags/') + 10).trim())
        .filter(line => line !== "");
}
async function getNextVersion(allTags, prefix) {
    const existingVersions = allTags
        .filter(tag => tag.startsWith(prefix))
        .map(tag => tag.substring(prefix.length));
    if (existingVersions.length === 0) {
        existingVersions.push('1.0.0');
    }
    if (process.env.GITHUB_REF_NAME === "main") {
        const maxVersion = semver.maxSatisfying(existingVersions, '*');
        return semver.inc(maxVersion, 'minor');
    }
    const maxVersion = semver.maxSatisfying(existingVersions, '*', { includePrerelease: true });
    return semver.inc(maxVersion, 'prerelease', 'beta', 1);
}
