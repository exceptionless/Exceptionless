/* eslint-env node */

"use strict";

var childProcess = require("child_process");
var fs = require("fs");
var path = require("path");

function getDevCertificate() {
    var baseFolder =
        process.env.APPDATA !== undefined && process.env.APPDATA !== ""
            ? path.join(process.env.APPDATA, "ASP.NET", "https")
            : path.join(process.env.HOME, ".aspnet", "https");
    var certificateName = process.env.npm_package_name;

    if (!certificateName) {
        throw new Error("Invalid certificate name. Run this command through an npm script.");
    }

    var certFilePath = path.join(baseFolder, certificateName + ".pem");
    var keyFilePath = path.join(baseFolder, certificateName + ".key");

    if (!fs.existsSync(certFilePath) || !fs.existsSync(keyFilePath)) {
        fs.mkdirSync(baseFolder, { recursive: true });
        childProcess.execFileSync("dotnet", [
            "dev-certs",
            "https",
            "--export-path",
            certFilePath,
            "--format",
            "Pem",
            "--no-password",
        ]);
    }

    return {
        cert: fs.readFileSync(certFilePath, "utf8"),
        key: fs.readFileSync(keyFilePath, "utf8"),
    };
}

module.exports = {
    getDevCertificate: getDevCertificate,
};
