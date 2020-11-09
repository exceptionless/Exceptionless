module.exports = {
    main: {
        src: ['temp/app.css', '<%= dom_munger.data.appcss %>'],
        dest: 'dist/app.min.css'
    }
};