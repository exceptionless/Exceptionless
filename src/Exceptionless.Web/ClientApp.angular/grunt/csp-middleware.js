/* eslint-env node */

"use strict";

var crypto = require("crypto");

var CSP_HEADER = "Content-Security-Policy";
var HTML_CACHE_CONTROL = "no-store";
var NONCE_BYTE_LENGTH = 32;
var SCRIPT_NONCE_PATTERN = /\s+nonce(?:\s*=\s*(?:"[^"]*"|'[^']*'|[^\s>]+))?/gi;
var SCRIPT_TAG_PATTERN = /<script\b((?:"[^"]*"|'[^']*'|[^'">])*)>/gi;

// Exceptionless uses Intercom's US endpoints. Keep region-specific sources scoped to that workspace.
var intercomChildSources = [
    "https://intercom-sheets.com",
    "https://www.intercom-reporting.com",
    "https://www.youtube.com",
    "https://player.vimeo.com",
    "https://fast.wistia.net",
];

var intercomDownloadSources = ["https://downloads.intercomcdn.com"];

var intercomUploadSources = ["https://uploads.intercomcdn.com", "https://uploads.intercomusercontent.com"];

var intercomAttachmentSources = [
    "https://*.intercom-attachments-1.com",
    "https://*.intercom-attachments-2.com",
    "https://*.intercom-attachments-3.com",
    "https://*.intercom-attachments-4.com",
    "https://*.intercom-attachments-5.com",
    "https://*.intercom-attachments-6.com",
    "https://*.intercom-attachments-7.com",
    "https://*.intercom-attachments-8.com",
    "https://*.intercom-attachments-9.com",
];

var contentSecurityPolicyDirectives = [
    ["default-src", ["'self'"]],
    [
        "script-src",
        [
            "'strict-dynamic'",
            "'self'",
            "https://js.stripe.com",
            "https://*.js.stripe.com",
            "https://maps.googleapis.com",
            "https://app.intercom.io",
            "https://widget.intercom.io",
            "https://js.intercomcdn.com",
            "https://cdn.jsdelivr.net",
        ],
    ],
    ["script-src-attr", ["'none'"]],
    ["style-src", ["'self'", "'unsafe-inline'", "https://fonts.googleapis.com", "https://cdn.jsdelivr.net"]],
    [
        "img-src",
        [
            "'self'",
            "blob:",
            "data:",
            "https://*.stripe.com",
            "https://*.link.com",
            "https://js.intercomcdn.com",
            "https://static.intercomassets.com",
            "https://gifs.intercomcdn.com",
            "https://video-messages.intercomcdn.com",
            "https://messenger-apps.intercom.io",
        ]
            .concat(intercomDownloadSources)
            .concat(intercomUploadSources)
            .concat(intercomAttachmentSources)
            .concat(["https://user-images.githubusercontent.com", "https://www.gravatar.com"]),
    ],
    [
        "font-src",
        [
            "'self'",
            "https://fonts.gstatic.com",
            "https://js.intercomcdn.com",
            "https://fonts.intercomcdn.com",
            "https://cdn.jsdelivr.net",
        ],
    ],
    [
        "connect-src",
        [
            "'self'",
            "https://collector.exceptionless.io",
            "https://config.exceptionless.io",
            "https://heartbeat.exceptionless.io",
            "https://api.stripe.com",
            "https://maps.googleapis.com",
            "https://link.com",
            "https://*.link.com",
            "https://via.intercom.io",
            "https://api.intercom.io",
            "https://api-iam.intercom.io",
            "https://api-ping.intercom.io",
            "https://*.intercom-messenger.com",
            "wss://*.intercom-messenger.com",
            "https://nexus-websocket-a.intercom.io",
            "wss://nexus-websocket-a.intercom.io",
            "https://nexus-websocket-b.intercom.io",
            "wss://nexus-websocket-b.intercom.io",
        ].concat(intercomUploadSources),
    ],
    [
        "frame-src",
        [
            "'self'",
            "https://js.stripe.com",
            "https://*.js.stripe.com",
            "https://hooks.stripe.com",
            "https://link.com",
            "https://*.link.com",
        ].concat(intercomChildSources),
    ],
    ["media-src", ["'self'", "blob:", "https://js.intercomcdn.com"].concat(intercomDownloadSources)],
    ["worker-src", ["'self'", "blob:"].concat(intercomChildSources)],
    ["form-action", ["'self'", "https://intercom.help", "https://api-iam.intercom.io"]],
    ["manifest-src", ["'self'"]],
    ["base-uri", ["'none'"]],
    ["object-src", ["'none'"]],
    ["frame-ancestors", ["'none'"]],
];

function createNonce() {
    return crypto.randomBytes(NONCE_BYTE_LENGTH).toString("base64");
}

function createContentSecurityPolicy(nonce) {
    return contentSecurityPolicyDirectives
        .map(function (directive) {
            var name = directive[0];
            var sources = directive[1].slice();

            if (name === "script-src") {
                sources.unshift("'nonce-" + nonce + "'");
            } else if (name === "connect-src") {
                sources.push("ws:", "wss:");
            }

            return name + " " + sources.join(" ");
        })
        .join("; ");
}

function stampScriptNonces(html, nonce) {
    return html.replace(SCRIPT_TAG_PATTERN, function (scriptTag, attributes) {
        var attributesWithoutNonce = attributes.replace(SCRIPT_NONCE_PATTERN, "");
        var scriptTagName = scriptTag.slice(0, "<script".length);

        return scriptTagName + ' nonce="' + nonce + '"' + attributesWithoutNonce + ">";
    });
}

function isHtmlRequest(request) {
    if (request.method !== "GET" && request.method !== "HEAD") {
        return false;
    }

    var headers = request.headers || {};
    var accept = headers.accept || "";
    var pathname = (request.url || "").split("?", 1)[0];
    var lastSegment = pathname.substring(pathname.lastIndexOf("/") + 1);

    return accept.indexOf("text/html") !== -1 || pathname === "/index.html" || lastSegment.indexOf(".") === -1;
}

function removeConditionalRequestHeaders(request) {
    if (!request.headers) {
        return;
    }

    delete request.headers["if-modified-since"];
    delete request.headers["if-none-match"];
}

function removeHeaderFromCollection(headers, name) {
    if (!headers || typeof headers !== "object") {
        return;
    }

    Object.keys(headers).forEach(function (headerName) {
        if (headerName.toLowerCase() === name) {
            delete headers[headerName];
        }
    });
}

function prepareHtmlHeaders(response, policy) {
    if (response.headersSent) {
        return;
    }

    response.setHeader(CSP_HEADER, policy);
    response.setHeader("Cache-Control", HTML_CACHE_CONTROL);
    response.removeHeader("Content-Length");
    response.removeHeader("ETag");
    response.removeHeader("Expires");
    response.removeHeader("Last-Modified");
}

function toBuffer(chunk, encoding) {
    if (Buffer.isBuffer(chunk)) {
        return chunk;
    }

    return Buffer.from(chunk, typeof encoding === "string" ? encoding : undefined);
}

function createCspMiddleware() {
    return function cspMiddleware(request, response, next) {
        if (!isHtmlRequest(request)) {
            next();
            return;
        }

        var nonce = createNonce();
        var policy = createContentSecurityPolicy(nonce);
        var chunks = [];
        var originalEnd = response.end;
        var originalWrite = response.write;
        var originalWriteHead = response.writeHead;

        removeConditionalRequestHeaders(request);
        prepareHtmlHeaders(response, policy);

        response.writeHead = function writeHead(statusCode, statusMessage, headers) {
            var args = [statusCode];
            var responseHeaders = headers;

            if (typeof statusMessage === "string") {
                args.push(statusMessage);
                if (headers) {
                    args.push(headers);
                }
            } else if (statusMessage) {
                responseHeaders = statusMessage;
                args.push(statusMessage);
            }

            removeHeaderFromCollection(responseHeaders, "content-length");
            removeHeaderFromCollection(responseHeaders, "etag");
            removeHeaderFromCollection(responseHeaders, "expires");
            removeHeaderFromCollection(responseHeaders, "last-modified");
            prepareHtmlHeaders(response, policy);

            return originalWriteHead.apply(response, args);
        };

        response.write = function write(chunk, encoding, callback) {
            var completed = typeof encoding === "function" ? encoding : callback;
            chunks.push(toBuffer(chunk, encoding));

            if (completed) {
                process.nextTick(completed);
            }

            return true;
        };

        response.end = function end(chunk, encoding, callback) {
            var completed = callback;
            if (typeof chunk === "function") {
                completed = chunk;
                chunk = undefined;
                encoding = undefined;
            } else if (typeof encoding === "function") {
                completed = encoding;
                encoding = undefined;
            }

            if (chunk !== undefined && chunk !== null) {
                chunks.push(toBuffer(chunk, encoding));
            }

            var html = Buffer.concat(chunks).toString("utf8");
            var body = Buffer.from(stampScriptNonces(html, nonce), "utf8");

            prepareHtmlHeaders(response, policy);
            if (!response.headersSent) {
                response.setHeader("Content-Length", body.length);
            }

            response.write = originalWrite;
            response.writeHead = originalWriteHead;
            response.end = originalEnd;

            return originalEnd.call(response, body, completed);
        };

        next();
    };
}

module.exports = {
    CSP_HEADER: CSP_HEADER,
    HTML_CACHE_CONTROL: HTML_CACHE_CONTROL,
    NONCE_BYTE_LENGTH: NONCE_BYTE_LENGTH,
    createContentSecurityPolicy: createContentSecurityPolicy,
    createCspMiddleware: createCspMiddleware,
    createNonce: createNonce,
    stampScriptNonces: stampScriptNonces,
};
