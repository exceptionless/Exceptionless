/* jslint node: true */
var puppeteer = require('puppeteer');
process.env.CHROME_BIN = puppeteer.executablePath();

module.exports = function (grunt) {
    return {
        options: {
            frameworks: ['jasmine'],
            files: [  //this files data is also updated in the watch handler, if updated change there too
                'node_modules/jquery/dist/jquery.js',
                'node_modules/boostrap/dist/js/bootstrap.js',
                '<%= dom_munger.data.appjs %>',
                'node_modules/angular-mocks/angular-mocks.js',
                grunt.option('folderGlobs')('*-spec.js'),

                'components/summary/**/*.html'
            ],
            ngHtml2JsPreprocessor: {
                moduleName: "app"
            },
            preprocessors: {
                'components/summary/**/*.html': ['ng-html2js']
            },
            logLevel: 'ERROR',
            reporters: ['mocha', 'junit'],
            junitReporter: {
              outputDir: 'results',
              outputFile: 'tests.xml',
              useBrowserName: false
            },
            autoWatch: false, //watching is handled by grunt-contrib-watch
            singleRun: true,

            port: Math.floor((Math.random() * 500) + 9500),
            browserDisconnectTimeout : 10000,
            browserDisconnectTolerance: 2,
            browserNoActivityTimeout: 60000
        },
        all_tests: {
            browsers: ['ChromeNoSandbox'],
            customLaunchers: {
              ChromeNoSandbox: {
                base: 'ChromeHeadless',
                flags: ['--no-sandbox']
              }
            }
        },
        during_watch: {
            browsers: ['ChromeNoSandbox'],
            customLaunchers: {
              ChromeNoSandbox: {
                base: 'ChromeHeadless',
                flags: ['--no-sandbox']
              }
            }
        }
    };
};
