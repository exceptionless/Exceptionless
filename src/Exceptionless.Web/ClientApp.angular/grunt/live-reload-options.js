/* eslint-env node */

"use strict";

module.exports = function createLiveReloadOptions(port, useHttps, certificate) {
    if (!useHttps) {
        return port;
    }

    return {
        cert: certificate.cert,
        key: certificate.key,
        port: port,
    };
};
