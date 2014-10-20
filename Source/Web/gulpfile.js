//Experimental

/*jslint node: true */
var gulp = require('gulp');
var concat = require('gulp-concat');
var connect = require('gulp-connect');
var uglify = require('gulp-uglify');
var imagemin = require('gulp-imagemin');
var less = require('gulp-less');
var gCheerio = require('gulp-cheerio');
var ngHtml2js = require("gulp-ng-html2js");
var ngAnnotate = require('gulp-ng-annotate');
var htmlmin = require('gulp-htmlmin');
var cssmin = require('gulp-cssmin');
var packagejson = require('./package.json');
var streamqueue = require('streamqueue');
var rimraf = require('rimraf');
var rename = require('gulp-rename');
var jshint = require('gulp-jshint');
var jasmine = require('gulp-jasmine');
var stylish = require('jshint-stylish');
var domSrc = require('gulp-dom-src');

var htmlminOptions = {
    collapseBooleanAttributes: true,
    collapseWhitespace: true,
    removeAttributeQuotes: true,
    removeComments: true,
    removeEmptyAttributes: true,
    // removeRedundantAttributes: true,
    removeScriptTypeAttributes: true,
    removeStyleLinkTypeAttributes: true
};

gulp.task('clean', function() {
    rimraf.sync('dist');
});

gulp.task('css', ['clean'], function() {
    return gulp.src('app.less')
        .pipe(less())
        .pipe(cssmin({keepSpecialComments: 0}))
        .pipe(rename('app.min.css'))
        .pipe(gulp.dest('dist/'));
});

gulp.task('js', ['clean'], function() {

    var templateStream = gulp.src(['!node_modules/**','!index.html','!_SpecRunner.html','!.grunt/**','!dist/**','!bower_components/**','**/*.html'])
        .pipe(htmlmin(htmlminOptions))
        .pipe(ngHtml2js({
            moduleName: packagejson.name
        }));

    var jsStream = domSrc({file:'index.html',selector:'script[data-build!="exclude"]',attribute:'src'});

    var combined = streamqueue({ objectMode: true });

    combined.queue(jsStream);
    combined.queue(templateStream);

    return combined.done()
        .pipe(concat('app.min.js'))
        .pipe(ngAnnotate())
        .pipe(uglify())
        .pipe(gulp.dest('dist/'));


    /*
        Should be able to add to an existing stream easier, like:
        gulp.src([... partials html ...])
          .pipe(htmlmin())
          .pipe(ngHtml2js())
          .pipe(domSrc(... js from script tags ...))  <-- add new files to existing stream
          .pipe(concat())
          .pipe(ngAnnotate())
          .pipe(uglify())
          .pipe(gulp.dest());

        https://github.com/wearefractal/vinyl-fs/issues/9
    */
});

gulp.task('indexHtml', ['clean'], function() {
    return gulp.src('index.html')
        .pipe(gCheerio(function ($) {
            $('script[data-remove!="exclude"]').remove();
            $('link').remove();
            $('body').append('<script src="app.min.js"></script>');
            $('head').append('<link rel="stylesheet" href="app.min.css">');
        }))
        .pipe(htmlmin(htmlminOptions))
        .pipe(gulp.dest('dist/'));
});

gulp.task('html', function () {
    gulp.src('!node_modules/**','!index.html','!_SpecRunner.html','!.grunt/**','!dist/**','!bower_components/**','**/*.html')
        .pipe(connect.reload());
});

gulp.task('images', ['clean'], function(){
    var combined = streamqueue({ objectMode: true });

    var appleImages = gulp.src(['favicon.ico', '*.png'])
        .pipe(imagemin())
        .pipe(gulp.dest('dist/'));

    var images = gulp.src('img/**')
        .pipe(imagemin())
        .pipe(gulp.dest('dist/img'));

    combined.queue(appleImages);
    combined.queue(images);

    return combined.done();
});

gulp.task('fonts', ['clean'], function(){
    var combined = streamqueue({ objectMode: true });

    var fontAwesome = gulp.src('bower_components/font-awesome/fonts/**')
        .pipe(gulp.dest('dist/bower_components/font-awesome/fonts/'));

    combined.queue(fontAwesome);

    return combined.done();
});

gulp.task('jshint', function(){
    gulp.src(['app.js', 'components/**/*.js','app/**/*.js'])
        .pipe(jshint())
        .pipe(jshint.reporter(stylish))
        .pipe(connect.reload());
});

gulp.task('build', ['clean', 'css', 'js', 'indexHtml', 'images', 'fonts']);

gulp.task('test', function() {
    return gulp.src(['components/**/*-spec.js','app/**/*-spec.js'])
        .pipe(jasmine())
        .on('error', function(err) {
            throw err;
        });
});

gulp.task('watch', function(){
    gulp.watch(['index.html', 'components/**/*.html','app/**/*.html'], ['html']);
    gulp.watch(['app.js', 'components/**/*.js','app/**/*.js'], ['jshint']);
});

gulp.task('connect', function() {
    connect.server({
        root: './',
        port: 9000,
        livereload: true
    });
});

gulp.task('default', ['connect', 'watch'], function() {

});

/*

-specifying clean dependency on each task is ugly
https://github.com/robrich/orchestrator/issues/26

-gulp-jasmine needs a phantomjs option
https://github.com/sindresorhus/gulp-jasmine/issues/2

*/
