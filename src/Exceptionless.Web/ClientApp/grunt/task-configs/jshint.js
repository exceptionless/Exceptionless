module.exports = function (grunt) {
    return {
        main: {
            options: {
                jshintrc: '.jshintrc'
            },
            src: grunt.option('folderGlobs')('*.js')
        }
    };
};