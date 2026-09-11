var devCertificate = require("../dev-certificate");

function createWatchConfig(grunt) {
    var liveReloadPort = Number(process.env.LIVERELOAD_PORT) || 35729;
    var useHttps = String(process.env.USE_HTTPS || "").toLowerCase() === "true";
    var certificate = useHttps ? devCertificate.getDevCertificate() : {};
    var liveReloadOptions = useHttps
        ? { cert: certificate.cert, key: certificate.key, port: liveReloadPort }
        : liveReloadPort;

    return {
        main: {
            options: {
                livereload: liveReloadOptions,
                livereloadOnError: false,
                spawn: false,
            },
            files: [grunt.option("folderGlobs")(["*.js", "*.less", "*.html"]), "!_SpecRunner.html", "!.grunt"],
            tasks: [], // all the tasks are run dynamically during the watch event handler
        },
    };
}

module.exports = createWatchConfig;
