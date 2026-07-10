/* eslint-env node */

"use strict";

var assert = require("node:assert/strict");
var test = require("node:test");

test("renders false, null, empty, nested, and HTML-like values without executable markup", function (context) {
    var directiveFactory;
    var documentStub = createDocumentStub();
    global.angular = {
        module: function () {
            return {
                directive: function (name, factory) {
                    assert.equal(name, "objectDump");
                    directiveFactory = factory;
                },
            };
        },
    };
    global.document = documentStub;
    context.after(function () {
        delete global.angular;
        delete global.document;
    });

    var modulePath = require.resolve("../components/object-dump/object-dump-directive");
    delete require.cache[modulePath];
    require("../components/object-dump/object-dump-directive");

    var root = documentStub.createElement("object-dump");
    var element = [root];
    element.text = function (value) {
        root.textContent = String(value);
    };
    var htmlLikeText = "<script>window.rendererDogfood = true;</script>";

    directiveFactory().link(
        {
            content: {
                false_value: false,
                null_value: null,
                empty_object: {},
                empty_array: [],
                nested: { html_like_text: htmlLikeText },
            },
        },
        element
    );

    assert.match(root.textContent, /false_valueFalse\r\n/);
    assert.match(root.textContent, /null_value\(Null\)\r\n/);
    assert.match(root.textContent, /empty_object\(Empty\)\r\n/);
    assert.match(root.textContent, /empty_array\(Empty\)\r\n/);
    assert.ok(root.textContent.includes(htmlLikeText));
    assert.equal(findElements(root, "SCRIPT").length, 0);

    var tables = findElements(root, "TABLE");
    assert.equal(tables.length, 2);
    assert.match(tables[0].className, /(?:^| )table-fixed(?: |$)/);
    assert.doesNotMatch(tables[1].className, /(?:^| )table-fixed(?: |$)/);
});

function createDocumentStub() {
    return {
        createElement: function (tagName) {
            return new ElementStub(tagName);
        },
        createTextNode: function (value) {
            return new TextNodeStub(value);
        },
    };
}

function ElementStub(tagName) {
    var self = this;
    this.tagName = tagName.toUpperCase();
    this.childNodes = [];
    this.className = "";
    this.classList = {
        add: function (name) {
            self.className = self.className ? self.className + " " + name : name;
        },
    };
}

ElementStub.prototype.appendChild = function (child) {
    this.childNodes.push(child);
    return child;
};

ElementStub.prototype.removeChild = function (child) {
    var index = this.childNodes.indexOf(child);
    if (index !== -1) {
        this.childNodes.splice(index, 1);
    }

    return child;
};

Object.defineProperty(ElementStub.prototype, "firstChild", {
    get: function () {
        return this.childNodes[0] || null;
    },
});

Object.defineProperty(ElementStub.prototype, "textContent", {
    get: function () {
        return this.childNodes
            .map(function (child) {
                return child.textContent;
            })
            .join("");
    },
    set: function (value) {
        this.childNodes = [new TextNodeStub(value)];
    },
});

function TextNodeStub(value) {
    this.textContent = String(value);
    this.childNodes = [];
}

function findElements(node, tagName) {
    var matches = node.tagName === tagName ? [node] : [];
    node.childNodes.forEach(function (child) {
        matches = matches.concat(findElements(child, tagName));
    });
    return matches;
}
