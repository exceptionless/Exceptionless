module.exports = function (grunt) {
    return {
        main: {
            options: {
                module: 'app',
                htmlmin: '<%= htmlmin.main.options %>'
            },
            src: [grunt.option('folderGlobs')('*.html'), '!index.html', '!_SpecRunner.html'],
            dest: 'temp/templates.js'
        }
    };
};