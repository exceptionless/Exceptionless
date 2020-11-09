module.exports = {
    before: {
        src: ['dist', 'temp']
    },
    after: {
        src: ['temp', 'dist/app.config.js', 'dist/app.min.css', 'dist/favicon.ico']
    }
};
