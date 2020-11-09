/* jslint node: true */
module.exports = {
  main: {
    options: {
      patterns: [
        {
          match: 'version',
          replacement: process.env.UI_VERSION || '2.0.0-dev'
        }
      ]
    },
    files: [
      {
        expand: true,
        flatten: true,
        src: ['dist/app.*.js'],
        dest: 'dist/'
      }
    ]
  }
};
