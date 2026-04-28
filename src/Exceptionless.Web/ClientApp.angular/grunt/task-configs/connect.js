var path = require("path");
var fs = require("fs");
var s = require("child_process");
// eslint-disable-next-line import/no-extraneous-dependencies
var proxyRequest = require("grunt-connect-proxy2/lib/utils").proxyRequest;

module.exports = function () {
    var target = getTarget();
    var useHttps = String(process.env.USE_HTTPS || "").toLowerCase() === "true";
    var port = Number(process.env.PORT) || 7121;
    var certs = useHttps ? generateCerts() : { cert: undefined, key: undefined };

    return {
        main: {
            options: {
                port: port,
                protocol: useHttps ? "https" : "http",
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
    var url = process.env.API_HTTPS || process.env.API_HTTP;

    if (url) {
        var parsed = new URL(url);
        var ssl = parsed.protocol === "https:";
        return {
            host: parsed.hostname,
            port: Number(parsed.port) || (ssl ? 443 : 80),
            ssl,
        };
    }

    return { host: "localhost", port: 7110, ssl: false };
}

function generateCerts() {
    var baseFolder =
        process.env.APPDATA !== undefined && process.env.APPDATA !== ""
            ? `${process.env.APPDATA}/ASP.NET/https`
            : `${process.env.HOME}/.aspnet/https`;
    var certificateName = process.env.npm_package_name;

    if (!certificateName) {
        // eslint-disable-next-line no-console
        console.error("Invalid certificate name. Run this script in the context of an npm script.");
        process.exit(-1);
    }

    var certFilePath = path.join(baseFolder, `${certificateName}.pem`);
    var keyFilePath = path.join(baseFolder, `${certificateName}.key`);

    if (!fs.existsSync(certFilePath) || !fs.existsSync(keyFilePath)) {
        var outp = s.execSync(`dotnet dev-certs https --export-path "${certFilePath}" --format Pem --no-password`);
        // eslint-disable-next-line no-console
        console.log(outp.toString());
    }

    return {
        cert: fs.readFileSync(certFilePath, "utf8"),
        key: fs.readFileSync(keyFilePath, "utf8"),
    };
}
