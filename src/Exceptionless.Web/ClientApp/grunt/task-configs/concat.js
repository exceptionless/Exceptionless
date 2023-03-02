module.exports = {
    main: {
        src: ['<%= dom_munger.data.appjs %>', '<%= ngtemplates.main.dest %>'],
        dest: 'dist/app.js'
    }
};