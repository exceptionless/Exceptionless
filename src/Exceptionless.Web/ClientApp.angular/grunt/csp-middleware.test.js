/* eslint-env node */

"use strict";

var assert = require("node:assert/strict");
var http = require("node:http");
var test = require("node:test");
var livereload = require("connect-livereload");
var csp = require("./csp-middleware");

function getScriptDirective(policy) {
    return getDirective(policy, "script-src");
}

function getDirective(policy, name) {
    return policy.split("; ").find(function (directive) {
        return directive.startsWith(name + " ");
    });
}

function getNonce(policy) {
    var match = getScriptDirective(policy).match(/'nonce-([^']+)'/);
    return match && match[1];
}

function getScriptTags(html) {
    return html.match(/<script\b[^>]*>/gi) || [];
}

function startServer() {
    var cspMiddleware = csp.createCspMiddleware();
    var livereloadMiddleware = livereload({ disableCompression: true, port: 35729 });
    var sourceHtml = [
        "<!doctype html>",
        "<html><body>",
        '<script nonce data-state="ready > pending" src="/first.js"></script>',
        '<script nonce="stale" src="/second.js"></script>',
        '<script nonce=also-stale src="/third.js"></script>',
        "<script>window.ready = true;</script>",
        "</body></html>",
    ].join("");

    var server = http.createServer(function (request, response) {
        cspMiddleware(request, response, function () {
            livereloadMiddleware(request, response, function () {
                response.setHeader("Content-Type", "text/html; charset=utf-8");
                response.end(sourceHtml);
            });
        });
    });

    return new Promise(function (resolve) {
        server.listen(0, "127.0.0.1", function () {
            resolve(server);
        });
    });
}

function getServerUrl(server) {
    return "http://127.0.0.1:" + server.address().port + "/";
}

test("creates a unique cryptographic nonce for each HTML response", async function (context) {
    var server = await startServer();
    context.after(function () {
        server.close();
    });

    var firstResponse = await fetch(getServerUrl(server), { headers: { Accept: "text/html" } });
    var secondResponse = await fetch(getServerUrl(server), { headers: { Accept: "text/html" } });
    var firstNonce = getNonce(firstResponse.headers.get(csp.CSP_HEADER));
    var secondNonce = getNonce(secondResponse.headers.get(csp.CSP_HEADER));

    assert.equal(Buffer.from(firstNonce, "base64").length, csp.NONCE_BYTE_LENGTH);
    assert.equal(Buffer.from(secondNonce, "base64").length, csp.NONCE_BYTE_LENGTH);
    assert.notEqual(firstNonce, secondNonce);
});

test("stamps every parser-inserted script after LiveReload injection", async function (context) {
    var server = await startServer();
    context.after(function () {
        server.close();
    });

    var response = await fetch(getServerUrl(server), { headers: { Accept: "text/html" } });
    var policy = response.headers.get(csp.CSP_HEADER);
    var nonce = getNonce(policy);
    var html = await response.text();
    var scriptTags = getScriptTags(html);

    assert.equal(scriptTags.length, 5);
    assert.match(html, /\/livereload\.js\?snipver=1/);
    assert.doesNotMatch(html, /nonce="stale"/);
    assert.doesNotMatch(html, /nonce=also-stale/);
    assert.match(html, /data-state="ready > pending"/);
    scriptTags.forEach(function (scriptTag) {
        var matches = scriptTag.match(/\snonce="([^"]+)"/g) || [];
        assert.deepEqual(matches, [' nonce="' + nonce + '"']);
    });
});

test("serves HTML with a strict script policy and no cache", async function (context) {
    var server = await startServer();
    context.after(function () {
        server.close();
    });

    var response = await fetch(getServerUrl(server), { headers: { Accept: "text/html" } });
    var policy = response.headers.get(csp.CSP_HEADER);
    var scriptDirective = getScriptDirective(policy);
    var styleDirective = getDirective(policy, "style-src");
    var connectDirective = getDirective(policy, "connect-src");
    var directiveNames = policy.split("; ").map(function (directive) {
        return directive.split(" ", 1)[0];
    });

    assert.equal(response.headers.get("Cache-Control"), csp.HTML_CACHE_CONTROL);
    assert.deepEqual(directiveNames, [
        "default-src",
        "script-src",
        "script-src-attr",
        "style-src",
        "img-src",
        "font-src",
        "connect-src",
        "frame-src",
        "media-src",
        "worker-src",
        "form-action",
        "manifest-src",
        "base-uri",
        "object-src",
        "frame-ancestors",
    ]);
    assert.equal(getDirective(policy, "default-src"), "default-src 'self'");
    assert.match(scriptDirective, /'nonce-[^']+'/);
    assert.match(scriptDirective, /'strict-dynamic'/);
    assert.doesNotMatch(scriptDirective, /'unsafe-inline'/);
    assert.doesNotMatch(scriptDirective, /'unsafe-eval'/);
    assert.match(policy, /script-src-attr 'none'/);
    assert.match(styleDirective, /'unsafe-inline'/);
    assert.match(connectDirective, /(?:^| )ws:(?: |$)/);
    assert.match(connectDirective, /(?:^| )wss:(?: |$)/);
    assert.doesNotMatch(policy, /(?:^|[ ;])http:/);
    assert.doesNotMatch(policy, /intercomcdn\.eu|\.eu\.intercom\.io|\.au\.intercom\.io|au\.intercomcdn\.com/);
    assert.doesNotMatch(
        policy,
        /static\.au\.intercomassets\.com|intercom-attachments\.eu|au\.intercom-attachments\.com/
    );
    assert.equal(getDirective(policy, "base-uri"), "base-uri 'none'");
    assert.equal(getDirective(policy, "object-src"), "object-src 'none'");
    assert.equal(getDirective(policy, "frame-ancestors"), "frame-ancestors 'none'");
});

test("leaves Angular template XHR caching unchanged", async function (context) {
    var server = await startServer();
    context.after(function () {
        server.close();
    });

    var response = await fetch(getServerUrl(server) + "app/auth/login.tpl.html", {
        headers: { Accept: "application/json, text/plain, */*" },
    });

    assert.equal(response.headers.get(csp.CSP_HEADER), null);
    assert.equal(response.headers.get("Cache-Control"), null);
});
