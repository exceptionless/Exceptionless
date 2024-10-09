var path = require("path");
var fs = require("fs");
var s = require("child_process");
// eslint-disable-next-line import/no-extraneous-dependencies
var proxyRequest = require("grunt-connect-proxy2/lib/utils").proxyRequest;

module.exports = function () {
    var target = getTarget();
    var certs = target.ssl ? generateCerts() : { cert: undefined, key: undefined };

    return {
        main: {
            options: {
                port: 5100,
                protocol: "http",
                key: certs.key,
                cert: certs.cert,
                middleware: function (connect, options, middlewares) {
                    middlewares.unshift(proxyRequest);
                    return middlewares;
                },
            },
            proxies: [
                {
                    context: "/api/v2/push",
                    host: target.host,
                    port: target.port,
                    ws: true,
                    secure: false,
                    https: target.ssl,
                },
                {
                    context: "/api",
                    host: target.host,
                    port: target.port,
                    secure: false,
                    https: target.ssl,
                },
                {
                    context: "/docs",
                    host: target.host,
                    port: target.port,
                    secure: false,
                    https: target.ssl,
                },
                {
                    context: "/health",
                    host: target.host,
                    port: target.port,
                    secure: false,
                    https: target.ssl,
                },
                {
                    context: "/metrics",
                    host: target.host,
                    port: target.port,
                    secure: false,
                    https: target.ssl,
                },
            ],
        },
    };
};

function getTarget() {
    var port = 5200;
    var host = "localhost";
    var ssl = false;

    if (process.env.ASPNETCORE_HTTPS_PORT) {
        port = process.env.ASPNETCORE_HTTPS_PORT;
    } else if (process.env.ASPNETCORE_URLS) {
        var url = process.env.ASPNETCORE_URLS.split(";")[0];
        var parts = url.split(":");
        if (url.startsWith("http://")) ssl = false;
        if (parts.length >= 2) host = parts[1].substring(2);
        if (parts.length >= 3) port = parts[2];
        else port = ssl ? 443 : 80;
    }

    return {
        port,
        host,
        ssl,
    };
}

/** Function taken from aspnetcore-https.js in ASP.NET React template https://github.com/microsoft/commercial-marketplace-offer-deploy/blob/main/src/ClientApp/ClientApp/aspnetcore-https.ts */
function generateCerts() {
    var baseFolder =
        process.env.APPDATA !== undefined && process.env.APPDATA !== ""
            ? `${process.env.APPDATA}/ASP.NET/https`
            : `${process.env.HOME}/.aspnet/https`;
    var certificateArg = process.argv
        .map((arg) => {
            var match = arg.match(/--name=(.+)/i);
            return match ? { value: match[1] } : null;
        })
        .filter(Boolean)[0];

    var certificateName = certificateArg ? certificateArg.groups.value : process.env.npm_package_name;

    if (!certificateName) {
        // eslint-disable-next-line no-console
        console.error(
            "Invalid certificate name. Run this script in the context of an npm/yarn script or pass --name=<<app>> explicitly."
        );
        process.exit(-1);
    }

    var certFilePath = path.join(baseFolder, `${certificateName}.pem`);
    var keyFilePath = path.join(baseFolder, `${certificateName}.key`);

    if (!fs.existsSync(certFilePath) || !fs.existsSync(keyFilePath)) {
        var outp = s.execSync(
            "dotnet " +
                ["dev-certs", "https", "--export-path", `"${certFilePath}"`, "--format", "Pem", "--no-password"].join(
                    " "
                )
        );
        // eslint-disable-next-line no-console
        console.log(outp.toString());
    }

    return {
        cert: fs.readFileSync(certFilePath, "utf8"),
        key: fs.readFileSync(keyFilePath, "utf8"),
    };
}
