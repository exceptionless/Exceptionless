module.exports = {
    main: {
        src: ["temp/app.css", "<%= dom_munger.data.appcss %>"],
        dest: "dist/app.min.css",
    },
    vendor: {
        src: [
            "node_modules/bootstrap/dist/css/bootstrap.min.css",
            "node_modules/font-awesome/css/font-awesome.min.css",
        ],
        dest: "dist/vendor.min.css",
    },
};
