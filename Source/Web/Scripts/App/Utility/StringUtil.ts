/// <reference path="../exceptionless.ts" />

module exceptionless {
    export class StringUtil {
        public static isNullOrEmpty(value: string) {
            return !value || value.length === 0;
        }

        // http://stackoverflow.com/questions/1038746/equivalent-of-string-format-in-jquery
        // format("i can speak {language} since i was {age}", { language:'JavaScript', age:10 });
        // format("i can speak {0} since i was {1}", 'JavaScript', 10);
        public static format(value: string, col: any) {
            col = typeof (col) === 'object' ? col : Array.prototype.slice.call(arguments, 1);

            // TODO: Fix once https://typescript.codeplex.com/workitem/1812 is resolved.
            return (<any>value).replace(/\{\{|\}\}|\{(\w+)\}/g, function (m, n) {
                if (m === "{{") { return "{"; }
                if (m === "}}") { return "}"; }

                return col[n] ? col[n] : '';
            });
        }

        public static startsWith(value: string, prefix: string) {
            return value.substr(0, prefix.length) === prefix;
        }

        public static endsWith(value: string, suffix: string) {
            return value.substr(value.length - suffix.length) === suffix;
        }
    }
}