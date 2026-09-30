module.exports = function (grunt) {
    var liveReloadPort = Number(process.env.LIVERELOAD_PORT) || 35729;

    return {
        main: {
            options: {
                livereload: liveReloadPort,
                livereloadOnError: false,
                spawn: false,
            },
            files: [grunt.option("folderGlobs")(["*.js", "*.less", "*.html"]), "!_SpecRunner.html", "!.grunt"],
            tasks: [], // all the tasks are run dynamically during the watch event handler
        },
    };
};
