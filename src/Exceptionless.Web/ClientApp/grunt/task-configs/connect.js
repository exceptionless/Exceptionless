var path = require('path');
var fs = require('fs');
var s = require('child_process');

module.exports = function () {
  var certs = generateCerts();
  var target = getTarget();

  return {
    main: {
      options: {
        port: 5100,
        protocol: 'https',
        key: certs.key,
        cert: certs.cert,
        middleware: function (connect, options, middlewares) {
          middlewares.unshift(require('grunt-connect-proxy2/lib/utils').proxyRequest);
          return middlewares;
        }
      },
      proxies: [
        {
          context: '/api/v2/push',
          host: target.host,
          port: target.port,
          ws: true,
          secure: false,
          https: target.ssl
        },
        {
          context: '/api',
          host: target.host,
          port: target.port,
          secure: false,
          https: target.ssl
        },
        {
          context: '/docs',
          host: target.host,
          port: target.port,
          secure: false,
          https: target.ssl
        },
        {
          context: '/health',
          host: target.host,
          port: target.port,
          secure: false,
          https: target.ssl
        },
        {
          context: '/metrics',
          host: target.host,
          port: target.port,
          secure: false,
          https: target.ssl
        }
      ]
    }
  }
};

function getTarget() {
  var port = 5201;
  var host = "localhost";
  var ssl = true;

  if (process.env.ASPNETCORE_HTTPS_PORT) {
    port = process.env.ASPNETCORE_HTTPS_PORT;
  } else if (process.env.ASPNETCORE_URLS) {
    var url = process.env.ASPNETCORE_URLS.split(';')[0];
    var parts = url.split(':');
    if (url.startsWith('http://'))
      ssl = false;
    if (parts.length >= 2)
      host = parts[1].substring(2);
    if (parts.length >= 3)
      port = parts[2];
    else
      port = ssl ? 443 : 80
  }

  return {
    port,
    host,
    ssl
  };
}

/** Function taken from aspnetcore-https.js in ASP.NET React template */
function generateCerts() {
  var baseFolder =
    process.env.APPDATA !== undefined && process.env.APPDATA !== ''
      ? `${process.env.APPDATA}/ASP.NET/https`
      : `${process.env.HOME}/.aspnet/https`;
  var certificateArg = process.argv
    .map((arg) => arg.match(/--name=(?<value>.+)/i))
    .filter(Boolean)[0];
  var certificateName = certificateArg
    ? certificateArg.groups.value
    : process.env.npm_package_name;

  if (!certificateName) {
    console.error(
      'Invalid certificate name. Run this script in the context of an npm/yarn script or pass --name=<<app>> explicitly.'
    );
    process.exit(-1);
  }

  var certFilePath = path.join(baseFolder, `${certificateName}.pem`);
  var keyFilePath = path.join(baseFolder, `${certificateName}.key`);

  if (!fs.existsSync(certFilePath) || !fs.existsSync(keyFilePath)) {
    var outp = s.execSync(
      'dotnet ' +
      [
        'dev-certs',
        'https',
        '--export-path',
        certFilePath,
        '--format',
        'Pem',
        '--no-password'
      ].join(' ')
    );
    console.log(outp.toString());
  }

  return {
    cert: fs.readFileSync(certFilePath, 'utf8'),
    key: fs.readFileSync(keyFilePath, 'utf8')
  };
}
