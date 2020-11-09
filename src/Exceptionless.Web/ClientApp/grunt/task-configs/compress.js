/* jslint node: true */
module.exports = function () {
  return {
    zip: {
      options: {
        archive: 'Exceptionless.UI.' + process.env.APPVEYOR_BUILD_VERSION + '.zip'
      },
      files: [
        {
          expand: true,
          cwd: 'dist/',
          src: ['**/*'],
          dest: './'
        }
      ]
    }
  };
};
