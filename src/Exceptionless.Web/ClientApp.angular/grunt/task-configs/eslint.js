module.exports = function (grunt) {
    return {
        main: {
            options: {},
            src: grunt.option("folderGlobs")("*.js"),
        },
    };
};
