/* global Handlebars:false */

(function () {
    "use strict";

    angular.module("exceptionless.object-dump").factory("handlebarsService", function () {
        var _defaultsRegistered = false;
        var _templates = {
            __default: "{{> valueDump}}",
            pre: "{{#ifHasData this}}<pre>{{> valueDump}}</pre>{{/ifHasData}}",
        };
        var _compiledTemplates = {};

        function getTemplate(templateKey) {
            if (!_defaultsRegistered) {
                registerDefaults();
                _defaultsRegistered = true;
            }

            // if we cant find a template with the given key, use the default template.
            if (!templateKey || !_templates.hasOwnProperty(templateKey)) templateKey = "__default";

            // check to see if we already have a compiled version of this template
            if (_compiledTemplates.hasOwnProperty(templateKey)) return _compiledTemplates[templateKey];

            var source = _templates[templateKey];
            var template = Handlebars.compile(source);

            // add the compiled template to the cache
            _compiledTemplates[templateKey] = template;
            return _compiledTemplates[templateKey];
        }

        function isEmpty(value) {
            if (value === null) return true;

            if (typeof value === "object" || value instanceof Object) {
                if (Object.keys(value).length > 0) {
                    return false;
                }
                return true;
            }

            if (Object.prototype.toString.call(value) === "[object Array]" || value instanceof Array) {
                if (value.length > 0) {
                    return false;
                }
                return true;
            }

            return false;
        }

        function reflect(value) {
            return {
                value: value,
                type: typeof value,
                isString: typeof value === "string" || value instanceof String,
                isNumber: typeof value === "number" || value instanceof Number,
                isBoolean: typeof value === "boolean" || value instanceof Boolean,
                isArray: Object.prototype.toString.call(value) === "[object Array]" || value instanceof Array,
                isObject: (typeof value === "object" || value instanceof Object) && value !== null,
                isNull: value === null,
                isEmptyValue: isEmpty(value),
                isSimpleType:
                    !(Object.prototype.toString.call(value) === "[object Array]" || value instanceof Array) &&
                    !(typeof value === "object" || value instanceof Object),
            };
        }

        function registerDefaults() {
            registerPartials();
            registerHelpers();
        }

        function registerHelpers() {
            Handlebars.registerHelper("reflect", function (context, options) {
                return options.fn(reflect(context));
            });

            Handlebars.registerHelper("properties", function (context, options) {
                var fn = options.fn;
                var ret = "";
                var data;
                if (options.data) data = Handlebars.createFrame(options.data);
                if (context) {
                    Object.keys(context || {})
                        .sort()
                        .forEach(function (key) {
                            var info = reflect(context[key]);
                            info.name = key;
                            ret += fn(info, { data: data });
                        });
                }
                return ret;
            });

            Handlebars.registerHelper("toSpaceWords", function (value) {
                if (value && value.length) {
                    value = value.replace(/_/g, " ").replace(/\s+/g, " ").trim();
                    value = value.replace(/([a-z0-9])([A-Z0-9])/g, "$1 $2");
                    value = value.length > 1 ? value.charAt(0).toUpperCase() + value.slice(1) : value;
                    return new Handlebars.SafeString(Handlebars.Utils.escapeExpression(value));
                }
                return "";
            });

            Handlebars.registerHelper("ifHasData", function (context, options) {
                if (isEmpty(context)) return options.inverse(this);
                return options.fn(this);
            });
        }

        function registerPartials() {
            Handlebars.registerPartial(
                "objectDump",
                "{{#ifHasData this}}" +
                    '<table class="table table-striped table-bordered table-fixed table-key-value b-t object-dump">\r\n' +
                    "{{#properties this}}" +
                    "  <tr>\r\n" +
                    "    <th>{{toSpaceWords name}}</th>\r\n" +
                    "    <td>\r\n" +
                    "      {{#with value}}{{> valueDump}}{{/with}}" +
                    "    </td>\r\n" +
                    "  </tr>\r\n" +
                    "{{/properties}}" +
                    "</table>\r\n" +
                    "{{/ifHasData}}"
            );

            Handlebars.registerPartial(
                "arrayDump",
                "{{#ifHasData this}}" +
                    "<ul>\r\n" +
                    "{{#each this}}" +
                    "  <li>{{> valueDump}}</li>\r\n" +
                    "{{/each}}" +
                    "</ul>\r\n" +
                    "{{/ifHasData}}"
            );

            Handlebars.registerPartial(
                "valueDump",
                "{{#reflect this}}" +
                    "{{#if isArray}}" +
                    "{{#with value}}{{> arrayDump}}{{/with}}" +
                    "{{else}}" +
                    "{{#if isObject}}" +
                    " {{#with value}}{{> objectDump}}{{/with}}" +
                    "{{else}}" +
                    "{{#if isBoolean}}" +
                    "{{#if value}}" +
                    "True\r\n" +
                    "{{else}}" +
                    "False\r\n" +
                    "{{/if}}" +
                    "{{else}}" +
                    "{{#if isNull}}" +
                    "(Null)\r\n" +
                    "{{else}}" +
                    "{{value}}\r\n" +
                    "{{/if}}" +
                    "{{/if}}" +
                    "{{/if}}" +
                    "{{/if}}" +
                    "{{/reflect}}"
            );
        }

        function registerTemplate(key, template) {
            _templates[key] = template;
        }

        var service = {
            getTemplate: getTemplate,
            isEmpty: isEmpty,
            reflect: reflect,
            registerDefaults: registerDefaults,
            registerHelpers: registerHelpers,
            registerPartials: registerPartials,
            registerTemplate: registerTemplate,
        };

        return service;
    });
})();
