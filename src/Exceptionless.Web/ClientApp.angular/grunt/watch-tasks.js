/* eslint-env node */

"use strict";

module.exports = function getWatchTasks(filepath) {
    var tasks = [];

    // HTML reloads directly; the legacy remote validator is excluded from builds and fails on current Node versions.
    if (filepath.endsWith(".js")) {
        tasks.push("eslint");
    }

    if (filepath === "index.html") {
        tasks.push("dom_munger:read");
    }

    return tasks;
};
