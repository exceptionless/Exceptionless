module.exports = {
    main: {
        files: [
            { src: [ 'app.config.js', '*.png', '*.ico', 'img/**/{*.png,*.jpg,*.ico}','lang/**' ], dest: 'dist/' }
        ]
    }
};
