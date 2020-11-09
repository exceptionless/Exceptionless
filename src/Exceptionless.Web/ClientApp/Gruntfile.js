/* jslint node: true */
'use strict';

var pkg = require('./package.json');

//Using exclusion patterns slows down Grunt significantly
//instead of creating a set of patterns like '**/*.js' and '!**/node_modules/**'
//this method is used to create a set of inclusive patterns for all subdirectories
//skipping node_modules, node_modules, dist, and any .dirs
//This enables users to create any directory structure they desire.
var createFolderGlobs = function (fileTypePatterns) {
  fileTypePatterns = Array.isArray(fileTypePatterns) ? fileTypePatterns : [fileTypePatterns];
  var ignore = ['node_modules', 'node_modules', 'dist', 'temp'];
  var fs = require('fs');
  return fs.readdirSync(process.cwd())
    .map(function (file) {
      if (ignore.indexOf(file) !== -1 ||
        file.indexOf('.') === 0 || !fs.lstatSync(file).isDirectory()) {
        return null;
      } else {
        return fileTypePatterns.map(function (pattern) {
          return file + '/**/' + pattern;
        });
      }
    })
    .filter(function (patterns) {
      return patterns;
    })
    .concat(fileTypePatterns);
};

module.exports = function (grunt) {
  var path = require('path');

  // load createFolderGlobs in options so it can be used across task configurations
  grunt.option('folderGlobs', createFolderGlobs);

  // load all task configurations
  require('load-grunt-config')(grunt, {
      configPath: path.join(process.cwd(), 'grunt/task-configs'),
      init: true,
      loadGruntTasks: {
          pattern: 'grunt-*',
          config: require('./package.json'),
          scope: 'devDependencies'
      }
  });

  grunt.registerTask('build', ['jshint', /* 'htmlangular', */ 'clean:before', 'less', 'dom_munger', 'ngtemplates', 'cssmin', 'concat', 'ngAnnotate', 'uglify', 'copy', 'htmlmin', 'replace', 'cacheBust', 'clean:after']);
  grunt.registerTask('default', ['build']);
  grunt.registerTask('zip', ['compress']);
  grunt.registerTask('publish', ['gh-pages']);
  grunt.registerTask('serve', ['dom_munger:read', 'jshint', 'connect', 'watch']);
  grunt.registerTask('test', ['dom_munger:read', 'karma:all_tests']);

  grunt.event.on('watch', function (action, filepath) {
    //https://github.com/gruntjs/grunt-contrib-watch/issues/156

    var tasksToRun = [];

    if (filepath.lastIndexOf('.html') !== -1 && filepath.lastIndexOf('.html') === filepath.length - 5) {
      //validate the changed html file
      grunt.config('htmlangular.main.files.src', [filepath]);
      tasksToRun.push('htmlangular');
    }

    if (filepath.lastIndexOf('.js') !== -1 && filepath.lastIndexOf('.js') === filepath.length - 3) {
      //lint the changed js file
      grunt.config('jshint.main.src', filepath);
      tasksToRun.push('jshint');

      //find the appropriate unit test for the changed file
      var spec = filepath;
      if (filepath.lastIndexOf('-spec.js') === -1 || filepath.lastIndexOf('-spec.js') !== filepath.length - 8) {
        spec = filepath.substring(0, filepath.length - 3) + '-spec.js';
      }

      //if the spec exists then lets run it
      if (grunt.file.exists(spec)) {
        var files = [].concat(grunt.config('dom_munger.data.appjs'));
        files.push('node_modules/angular-mocks/angular-mocks.js');
        files.push(spec);
        grunt.config('karma.options.files', files);
        tasksToRun.push('karma:during_watch');
      }
    }

    //if index.html changed, we need to reread the <script> tags so our next run of karma
    //will have the correct environment
    if (filepath === 'index.html') {
      tasksToRun.push('dom_munger:read');
    }

    grunt.config('watch.main.tasks', tasksToRun);
  });
};
