module.exports = {
    main: {
        options: {
            collapseBooleanAttributes: true,
            collapseWhitespace: true,
            conservativeCollapse: true,
            removeAttributeQuotes: false,
            removeComments: true,
            removeEmptyAttributes: true,
            removeScriptTypeAttributes: true,
            removeStyleLinkTypeAttributes: true
        },
        files: {
            'dist/index.html': 'dist/index.html'
        }
    }
};