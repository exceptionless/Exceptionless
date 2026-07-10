var devCertificate = require("../dev-certificate");
var createLiveReloadOptions = require("../live-reload-options");

function createWatchConfig(grunt) {
    var liveReloadPort = Number(process.env.LIVERELOAD_PORT) || 35729;
    var useHttps = String(process.env.USE_HTTPS || "").toLowerCase() === "true";
    var certificate = useHttps ? devCertificate.getDevCertificate() : {};

    return {
        main: {
            options: {
                livereload: createLiveReloadOptions(liveReloadPort, useHttps, certificate),
                livereloadOnError: false,
                spawn: false,
            },
            files: [grunt.option("folderGlobs")(["*.js", "*.less", "*.html"]), "!_SpecRunner.html", "!.grunt"],
            tasks: [], // all the tasks are run dynamically during the watch event handler
        },
    };
}

module.exports = createWatchConfig;
