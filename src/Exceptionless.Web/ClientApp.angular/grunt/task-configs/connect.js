// eslint-disable-next-line import/no-extraneous-dependencies
var livereload = require("connect-livereload");
// eslint-disable-next-line import/no-extraneous-dependencies
var proxyRequest = require("grunt-connect-proxy2/lib/utils").proxyRequest;
var csp = require("../csp-middleware");
var devCertificate = require("../dev-certificate");

module.exports = function () {
    var target = getTarget();
    var useHttps = String(process.env.USE_HTTPS || "").toLowerCase() === "true";
    var port = Number(process.env.PORT) || 7121;
    var liveReloadPort = Number(process.env.LIVERELOAD_PORT) || 35729;
    var certs = useHttps ? devCertificate.getDevCertificate() : { cert: undefined, key: undefined };

    return {
        main: {
            options: {
                port: port,
                protocol: useHttps ? "https" : "http",
                key: certs.key,
                cert: certs.cert,
                middleware: function (connect, options, middlewares) {
                    middlewares.unshift(proxyRequest);
                    middlewares.splice(
                        1,
                        0,
                        csp.createCspMiddleware(),
                        livereload({ disableCompression: true, port: liveReloadPort })
                    );
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
