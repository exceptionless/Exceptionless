module.exports = {
    options: {
        sourceMap: true,
        sourceMapIncludeSources: false,
        mangle: {
            reserved: ["$super"],
        },
    },
    main: {
        src: "dist/app.js",
        dest: "dist/app.min.js",
    },
    vendor: {
        src: [
            "node_modules/jquery/dist/jquery.min.js",
            "node_modules/bootstrap/dist/js/bootstrap.min.js",
            "node_modules/lodash/lodash.min.js",
        ],
        dest: "dist/vendor.min.js",
    },
};
