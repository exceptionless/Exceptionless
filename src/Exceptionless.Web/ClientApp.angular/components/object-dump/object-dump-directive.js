(function () {
    "use strict";

    angular.module("exceptionless.object-dump").directive("objectDump", function () {
        function isArray(value) {
            return Object.prototype.toString.call(value) === "[object Array]" || value instanceof Array;
        }

        function isObject(value) {
            return (typeof value === "object" || value instanceof Object) && value !== null && !isArray(value);
        }

        function isEmpty(value) {
            return (isArray(value) || isObject(value)) && Object.keys(value).length === 0;
        }

        function formatValue(value) {
            if (typeof value === "boolean" || value instanceof Boolean) {
                return value ? "True\r\n" : "False\r\n";
            }

            if (value === null) {
                return "(Null)\r\n";
            }

            return String(value) + "\r\n";
        }

        function renderArray(value, depth) {
            var list = document.createElement("ul");

            value.forEach(function (item) {
                var listItem = document.createElement("li");
                listItem.appendChild(renderValue(item, depth + 1));
                list.appendChild(listItem);
            });

            return list;
        }

        function renderObject(value, depth) {
            var table = document.createElement("table");
            table.className = "table table-striped table-bordered table-key-value b-t object-dump";
            if (depth === 0) {
                table.classList.add("table-fixed");
            }

            Object.keys(value).forEach(function (key) {
                var row = document.createElement("tr");
                var heading = document.createElement("th");
                var cell = document.createElement("td");

                heading.textContent = key;
                cell.appendChild(renderValue(value[key], depth + 1));
                row.appendChild(heading);
                row.appendChild(cell);
                table.appendChild(row);
            });

            return table;
        }

        function renderValue(value, depth) {
            if (isEmpty(value)) {
                return document.createTextNode("(Empty)\r\n");
            }

            if (isArray(value)) {
                return renderArray(value, depth);
            }

            if (isObject(value)) {
                return renderObject(value, depth);
            }

            return document.createTextNode(formatValue(value));
        }

        function replaceContent(element, content) {
            while (element.firstChild) {
                element.removeChild(element.firstChild);
            }

            element.appendChild(content);
        }

        return {
            restrict: "E",
            scope: {
                content: "=content",
                templateKey: "=templateKey",
            },
            link: function (scope, element) {
                if (typeof scope.content === "undefined") {
                    return;
                }

                try {
                    var content = scope.content;
                    var usePreformattedText = scope.templateKey === "pre";

                    if (typeof content === "string" || content instanceof String) {
                        try {
                            content = JSON.parse(scope.content);
                        } catch (ex) {
                            usePreformattedText = true;
                        }
                    }

                    var renderedContent = renderValue(content, 0);
                    if (usePreformattedText && !isEmpty(content)) {
                        var pre = document.createElement("pre");
                        pre.appendChild(renderedContent);
                        renderedContent = pre;
                    }

                    replaceContent(element[0], renderedContent);
                } catch (ex) {
                    element.text(scope.content);
                }
            },
        };
    });
})();
