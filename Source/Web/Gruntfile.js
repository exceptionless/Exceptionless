/*jslint node: true */
'use strict';

var pkg = require('./package.json');

//Using exclusion patterns slows down Grunt significantly
//instead of creating a set of patterns like '**/*.js' and '!**/node_modules/**'
//this method is used to create a set of inclusive patterns for all subdirectories
//skipping node_modules, bower_components, dist, and any .dirs
//This enables users to create any directory structure they desire.
var createFolderGlobs = function (fileTypePatterns) {
  fileTypePatterns = Array.isArray(fileTypePatterns) ? fileTypePatterns : [fileTypePatterns];
  var ignore = ['node_modules', 'bower_components', 'dist', 'temp'];
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

  // load all grunt tasks
  require('load-grunt-tasks')(grunt);
  grunt.loadNpmTasks('grunt-ng-constant');
  grunt.loadNpmTasks('grunt-html-angular-validate');

  // Project configuration.
  grunt.initConfig({
    connect: {
      main: {
        options: {
          port: 9001
        }
      }
    },
    watch: {
      main: {
        options: {
          livereload: true,
          livereloadOnError: false,
          spawn: false
        },
        files: [createFolderGlobs(['*.js', '*.less', '*.html']), '!_SpecRunner.html', '!.grunt'],
        tasks: [] //all the tasks are run dynamically during the watch event handler
      }
    },
    jshint: {
      main: {
        options: {
          jshintrc: '.jshintrc'
        },
        src: createFolderGlobs('*.js')
      }
    },
    htmlangular: {
      main: {
        options: {
          customtags: ['date-*', 'events', 'progressbar', 'project-filter', 'projects', 'rickshaw', 'search-filter', 'simple-stack-trace', 'stacks', 'stack-trace', 'summary', 'timeago', 'toaster-container'],
          customattrs: [
            'is-*',
            'ui-*',
            'checklist-*',
            'refresh-*',
            'typeahead',
            'truncate',
            'lines'
          ],
          tmplext: 'tpl.html',
          reportpath: null,
          relaxerror: [
            'Element img is missing required attribute src.',
            'Element tabset not allowed as child of element div in this context.',
            'Element div not allowed as child of element pre in this context.'
          ]
        },
        files: {
          src: ['index.html', 'app/**/*.html', 'components/**/*.html']
        }
      }
    },
    clean: {
      before: {
        src: ['dist', 'temp']
      },
      after: {
        src: ['temp']
      }
    },
    less: {
      main: {
        options: {},
        files: {
          'temp/app.css': 'app.less'
        }
      }
    },
    ngconstant: {
      main: {
        options: {
          name: 'app.config',
          space: '  ',
          wrap: '(function () {\n  "use strict";\n\n  {%= __ngModule %}\n}());',
          dest: 'dist/app.config.js',
          constants: {
            VERSION: process.env.BUILD_NUMBER ? process.env.BUILD_NUMBER : '2.0.0',
            BASE_URL: process.env.API_URL ? process.env.API_URL : 'http://localhost:50000'
          }
        },
        build: {}
      }
    },
    ngtemplates: {
      main: {
        options: {
          module: 'app',
          htmlmin: '<%= htmlmin.main.options %>'
        },
        src: [createFolderGlobs('*.html'), '!index.html', '!_SpecRunner.html'],
        dest: 'temp/templates.js'
      }
    },
    copy: {
      main: {
        files: [
          {src: ['*.png', '*.ico', 'img/**/{*.png,*.jpg,*.ico}'], dest: 'dist/'}
        ]
      }
    },
    dom_munger: {
      read: {
        options: {
          read: [
            {selector: 'script[data-concat!="false"]', attribute: 'src', writeto: 'appjs'},
            {selector: 'link[rel="stylesheet"][data-concat="true"]', attribute: 'href', writeto: 'appcss'}
          ]
        },
        src: 'index.html'
      },
      update: {
        options: {
          remove: ['script[data-remove!="false"]', 'link[data-remove="true"]'],
          append: [
            {
              selector: 'body',
              html: '<script src="app.min.js"></script><script src="app.config.js"></script>'
            },
            {selector: 'head', html: '<link rel="stylesheet" href="app.min.css">'}
          ]
        },
        src: 'index.html',
        dest: 'dist/index.html'
      }
    },
    cssmin: {
      main: {
        src: ['temp/app.css', '<%= dom_munger.data.appcss %>'],
        dest: 'dist/app.min.css'
      }
    },
    concat: {
      main: {
        src: ['<%= dom_munger.data.appjs %>', '<%= ngtemplates.main.dest %>'],
        dest: 'dist/app.js'
      }
    },
    ngAnnotate: {
      options: {
        singleQuotes: true
      },
      main: {
        files: {
          'dist/app.js': ['dist/app.js']
        }
      }
    },
    uglify: {
      options: {
        sourceMap: true,
        sourceMapIncludeSources: false,
        mangle: {
          except: ['$super']
        }
      },
      main: {
        src: 'dist/app.js',
        dest: 'dist/app.min.js'
      }
    },
    htmlmin: {
      main: {
        options: {
          collapseBooleanAttributes: true,
          collapseWhitespace: true,
          removeAttributeQuotes: true,
          removeComments: true,
          removeEmptyAttributes: true,
          removeScriptTypeAttributes: true,
          removeStyleLinkTypeAttributes: true
        },
        files: {
          'dist/index.html': 'dist/index.html'
        }
      }
    },
    karma: {
      options: {
        frameworks: ['jasmine'],
        files: [  //this files data is also updated in the watch handler, if updated change there too
          'bower_components/jquery/dist/jquery.js',
          'bower_components/boostrap/dist/js/bootstrap.js',
          '<%= dom_munger.data.appjs %>',
          'bower_components/angular-mocks/angular-mocks.js',
          createFolderGlobs('*-spec.js'),

          'components/event-summary/**/*.html'
        ],
        ngHtml2JsPreprocessor: {
          moduleName: "app"
        },
        preprocessors: {
          'components/summary/**/*.html': 'ng-html2js'
        },
        logLevel: 'ERROR',
        reporters: ['mocha'],
        autoWatch: false, //watching is handled by grunt-contrib-watch
        singleRun: true
      },
      all_tests: {
        browsers: ['PhantomJS']//,'Chrome','Firefox']
      },
      during_watch: {
        browsers: ['PhantomJS']
      }
    }
  });

  grunt.registerTask('build', ['jshint', /* 'htmlangular', */ 'clean:before', 'less', 'dom_munger', 'ngconstant', 'ngtemplates', 'cssmin', 'concat', 'ngAnnotate', 'uglify', 'copy', 'htmlmin', 'clean:after']);
  grunt.registerTask('default', ['build']);
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
        files.push('bower_components/angular-mocks/angular-mocks.js');
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
