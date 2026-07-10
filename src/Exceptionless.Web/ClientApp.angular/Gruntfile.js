/* jslint node: true */

"use strict";

// Using exclusion patterns slows down Grunt significantly
// instead of creating a set of patterns like '**/*.js' and '!**/node_modules/**'
// this method is used to create a set of inclusive patterns for all subdirectories
// skipping node_modules, dist, and any .dirs
// This enables users to create any directory structure they desire.
var createFolderGlobs = function (fileTypePatterns) {
    fileTypePatterns = Array.isArray(fileTypePatterns) ? fileTypePatterns : [fileTypePatterns];
    var ignore = ["node_modules", "dist", "temp", "grunt"];
    var fs = require("fs");
    return fs
        .readdirSync(process.cwd())
        .map(function (file) {
            if (ignore.indexOf(file) !== -1 || file.indexOf(".") === 0 || !fs.lstatSync(file).isDirectory()) {
                return null;
            }
            return fileTypePatterns.map(function (pattern) {
                return file + "/**/" + pattern;
            });
        })
        .filter(function (patterns) {
            return patterns;
        })
        .concat(fileTypePatterns);
};

function configureGrunt(grunt) {
    var path = require("path");
    var getWatchTasks = require("./grunt/watch-tasks");

    // load createFolderGlobs in options so it can be used across task configurations
    grunt.option("folderGlobs", createFolderGlobs);

    // load all task configurations
    require("load-grunt-config")(grunt, {
        configPath: path.join(process.cwd(), "grunt/task-configs"),
        init: true,
        loadGruntTasks: {
            pattern: "grunt-*",
            config: require("./package.json"),
            scope: "devDependencies",
        },
    });

    grunt.registerTask("build", [
        "eslint",
        /* 'htmlangular', */ "clean:before",
        "less",
        "dom_munger",
        "ngtemplates",
        "cssmin",
        "concat",
        "ngAnnotate",
        "uglify",
        "copy",
        "htmlmin",
        "replace",
        "cacheBust",
        "clean:after",
    ]);
    grunt.registerTask("default", ["build"]);
    grunt.registerTask("serve", ["dom_munger:read", "eslint", "configureProxies:main", "connect", "watch"]);

    grunt.event.on("watch", function (action, filepath) {
        // https://github.com/gruntjs/grunt-contrib-watch/issues/156

        var tasksToRun = getWatchTasks(filepath);

        if (tasksToRun.indexOf("eslint") !== -1) {
            // lint the changed js file
            grunt.config("eslint.main.src", filepath);
        }

        grunt.config("watch.main.tasks", tasksToRun);
    });
}

module.exports = configureGrunt;
