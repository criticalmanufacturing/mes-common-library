#!/usr/bin/env node

"use strict";

const path = require('path'),
    mkdirp = require('mkdirp'),
    rimraf = require('rimraf'),
    fs = require('fs'),
    dbg = require('debug'),
    node_modules = require('node_modules-path'),
    { parsePackageJson, PLATFORM_MAPPING, ARCH_MAPPING } = require('./utils');


const debug = dbg("cmf:debug");

function getInstallationPath(opts) {
    debug("Getting installation path...");
    debug(`Targeting node_modules/.bin/${opts.binBaseName}. Making sure path exists...`);
    const dir = path.join(node_modules(), ".bin", opts.binBaseName);
    mkdirp.sync(dir);
    debug(`Install path exists!`);
    return dir;
}

async function verifyAndPlaceBinary(binName, binPath, callback) {
    if (!fs.existsSync(path.join(binPath, binName))) return callback('Downloaded binary does not contain the binary specified in configuration - ' + binName);
    return callback(null);
}

/**
 * Reads the configuration from application's package.json,
 * validates properties, copied the binary from the package and stores at
 * ./bin in the package's root. NPM already has support to install binary files
 * specific locations when invoked with "npm install -g"
 *
 *  See: https://docs.npmjs.com/files/package.json#bin
 */
var INVALID_INPUT = "Invalid inputs";
async function install(callback) {
    var opts = parsePackageJson(".");
    if (!opts) return callback(INVALID_INPUT);
    console.info(`Copying the relevant binary for your platform ${process.platform}`);
    const src = path.join(__dirname, "dist", `${PLATFORM_MAPPING[process.platform]}-${ARCH_MAPPING[process.arch]}`);

    const installPath = getInstallationPath(opts);
    debug(`Copying ${src} to ${installPath}`);
    fs.cpSync(src, installPath, { recursive: true, force: true });
    if (process.platform !== "win32") {
        fs.chmodSync(path.join(installPath, opts.binName), 0o755);
    }

    await verifyAndPlaceBinary(opts.binName, installPath, callback);
}

async function uninstall(callback) {
    var opts = parsePackageJson(".");
    if (!opts) return callback(INVALID_INPUT);
    try {
        const installationPath = getInstallationPath(opts);
        debug("Deleting binaries from " + installationPath);
        rimraf.sync(installationPath);
    } catch (ex) {
        // Ignore errors when deleting the file.
        console.warn(ex);
    }
    console.info(`Uninstalled ${opts.binName} successfully`);
    return callback(null);
}

// Parse command line arguments and call the right method
var actions = {
    "install": install,
    "uninstall": uninstall
};

var argv = process.argv;
if (argv && argv.length > 2) {
    var cmd = process.argv[2];
    if (!actions[cmd]) {
        console.log("Invalid command. `install` and `uninstall` are the only supported commands");
        process.exit(1);
    }

    actions[cmd](function (err) {
        if (err) {
            console.error(err);
            process.exit(1);
        } else {
            process.exit(0);
        }
    }).catch(function (err) {
        console.error(err);
        process.exit(1);
    });
}
