/// <reference path="../exceptionless.ts" />

module exceptionless {
    export class HandlebarsUtil {
        private static _defaultsRegistered : boolean = false;

        private static _templates = { __default: '{{> valueDump}}' };

        private static _compiledTemplates = {};
        public static registerTemplate(key: string, template: string) {
            HandlebarsUtil._templates[key] = template;
        }

        public static getTemplate(value: any): any {
            if (!HandlebarsUtil._defaultsRegistered) {
                HandlebarsUtil.registerDefaults();
                HandlebarsUtil._defaultsRegistered = true;
            }

            // get template key from attribute
            var templateKey = $(value).attr('data-bind-template');

            // if we cant find a template with the given key, use the default template.
            if (!HandlebarsUtil._templates.hasOwnProperty(templateKey))
                templateKey = '__default';

            // check to see if we already have a compiled version of this template
            if (HandlebarsUtil._compiledTemplates.hasOwnProperty(templateKey))
                return HandlebarsUtil._compiledTemplates[templateKey];

            var source = HandlebarsUtil._templates[templateKey];
            var template = Handlebars.compile(source);

            // add the compliled template to the cache
            HandlebarsUtil._compiledTemplates[templateKey] = template;

            return HandlebarsUtil._compiledTemplates[templateKey];
        }

        public static registerDefaults() {
            HandlebarsUtil.registerPartials();
            HandlebarsUtil.registerHelpers();
        }

        private static registerPartials() {
            Handlebars.registerPartial('objectDump',
                '{{#ifHasData this}}' +
                '<table class="table table-bordered table-striped table-fixed keyvalue object-dump">\r\n' +
                '{{#properties this}}' +
                '  <tr>\r\n' +
                    '    <th>{{toSpaceWords name}}</th>\r\n' +
                    '    <td>\r\n' +
                    '      {{#with value}}{{> valueDump}}{{/with}}' +
                    '    </td>\r\n' +
                '  </tr>\r\n' +
                '{{/properties}}' +
                '</table>\r\n' +
                '{{/ifHasData}}');

            Handlebars.registerPartial('arrayDump',
                '{{#ifHasData this}}' +
                '<ul>\r\n' +
                    '{{#each this}}' +
                    '  <li>{{> valueDump}}</li>\r\n' +
                    '{{/each}}' +
                '</ul>\r\n' +
                '{{/ifHasData}}');

            Handlebars.registerPartial('valueDump',
                '{{#reflect this}}' +
                    '{{#if isArray}}' +
                        '{{#with value}}{{> arrayDump}}{{/with}}' +
                    '{{else}}' +
                      '{{#if isObject}}' +
                        ' {{#with value}}{{> objectDump}}{{/with}}' +
                      '{{else}}' +
                        '{{#if isBoolean}}' +
                            '{{#if value}}' +
                                'True\r\n' +
                            '{{else}}' +
                                'False\r\n' +
                            '{{/if}}' +
                        '{{else}}' +
                            '{{#if isNull}}' +
                                '(Null)\r\n' +
                            '{{else}}' +
                                '{{value}}\r\n' +
                            '{{/if}}' +
                        '{{/if}}' +
                      '{{/if}}' +
                    '{{/if}}' +
                '{{/reflect}}');
        }

        private static reflect(value): any {
            return {
                value: value,
                type: typeof value,
                isString: (typeof value === 'string' || value instanceof String),
                isNumber: (typeof value === 'number' || value instanceof Number),
                isBoolean: (typeof value === 'boolean' || value instanceof Boolean),
                isArray: (typeof value === 'array' || value instanceof Array),
                isObject: (typeof value === 'object' || value instanceof Object) && value !== null,
                isNull: value === null,
                isEmptyValue: HandlebarsUtil.isEmpty(value),
                isSimpleType: !(typeof value === 'array' || value instanceof Array) && !(typeof value === 'object' || value instanceof Object)
            };
        }

        private static isEmpty(value): boolean {
            if (value === null)
                return true;

            if (typeof value === 'object' || value instanceof Object) {
                if (Object.keys(value).length > 0) {
                    return false;
                }
                return true;
            }

            if (typeof value === 'array' || value instanceof Array) {
                if (value.length > 0) {
                    return false;
                }
                return true;
            }

            return false;
        }

        private static registerHelpers() {
            Handlebars.registerHelper('reflect', function (context, options) {
                return options.fn(HandlebarsUtil.reflect(context));
            });

            Handlebars.registerHelper('properties', function (context, options) {
                var fn = options.fn;
                var ret = '', data;

                if (options.data)
                    data = Handlebars.createFrame(options.data);

                if (context) {
                    for (var field in context) {
                        var info = HandlebarsUtil.reflect(context[field]);
                        info.name = field;

                        ret = ret + fn(info, { data: data });
                    }
                }
                return ret;
            });

            Handlebars.registerHelper('toSpaceWords', function (value) {
                if (value) {
                    if (!value.match(/\d+|__/g)) {
                        value = value.replace(/([a-z])([A-Z])/g, '$1 $2');
                        value = value.length > 1 ? value.charAt(0).toUpperCase() + value.slice(1) : value;
                    }

                    return new Handlebars.SafeString((<any>Handlebars).Utils.escapeExpression(value));
                } else {
                    return '';
                }
            });

            Handlebars.registerHelper('ifHasData', function (context, options) {
                if (HandlebarsUtil.isEmpty(context))
                    return options.inverse(this);
                else
                    return options.fn(this);
            });
        }
    }
}