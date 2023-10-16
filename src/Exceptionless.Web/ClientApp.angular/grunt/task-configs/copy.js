module.exports = {
    main: {
        files: [
            { src: ["app.config.js", "*.png", "*.ico", "img/**/{*.png,*.jpg,*.ico}", "lang/**"], dest: "dist/" },
            { src: ["node_modules/font-awesome/fonts/**"], dest: "dist/fonts/", expand: true, flatten: true, filter: "isFile" },
        ],
    },
};
