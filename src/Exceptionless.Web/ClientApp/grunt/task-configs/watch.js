module.exports = function (grunt) {
    return {
        main: {
            options: {
                livereload: true,
                livereloadOnError: false,
                spawn: false
            },
            files: [grunt.option('folderGlobs')(['*.js', '*.less', '*.html']), '!_SpecRunner.html', '!.grunt'],
            tasks: [] //all the tasks are run dynamically during the watch event handler
        }
    };
};