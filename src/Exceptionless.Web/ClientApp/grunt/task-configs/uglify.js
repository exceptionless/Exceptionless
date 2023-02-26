module.exports = {
    options: {
        sourceMap: true,
        sourceMapIncludeSources: false,
        mangle: {
          reserved: ['$super']
        }
    },
    main: {
        src: 'dist/app.js',
        dest: 'dist/app.min.js'
    }
};
