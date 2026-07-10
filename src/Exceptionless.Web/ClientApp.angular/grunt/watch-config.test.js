/* eslint-env node */

"use strict";

var assert = require("node:assert/strict");
var test = require("node:test");
var createLiveReloadOptions = require("./live-reload-options");
var getWatchTasks = require("./watch-tasks");

test("uses the numeric LiveReload port for HTTP development", function () {
    assert.equal(createLiveReloadOptions(35729, false, {}), 35729);
});

test("uses the development certificate for HTTPS LiveReload", function () {
    assert.deepEqual(createLiveReloadOptions(35729, true, { cert: "certificate", key: "private-key" }), {
        cert: "certificate",
        key: "private-key",
        port: 35729,
    });
});

test("reloads HTML without invoking the obsolete remote validator", function () {
    assert.deepEqual(getWatchTasks("app/app.tpl.html"), []);
    assert.deepEqual(getWatchTasks("index.html"), ["dom_munger:read"]);
});

test("still lints changed JavaScript", function () {
    assert.deepEqual(getWatchTasks("components/example.js"), ["eslint"]);
});
