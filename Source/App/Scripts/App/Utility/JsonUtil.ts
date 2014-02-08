/// <reference path="../exceptionless.ts" />

module exceptionless {
    export class JsonUtil {
        constructor () { }

        public static toTable(data: string): JQuery {
            var json = JSON.parse(data);
            return JsonUtil.createTableRecursive(json);
        }

        private static createTableRecursive(data: any): JQuery {
            var table = $('<table class="table table-condensed"><thead><th>Name</th><th>Value</th></thead><tbody>');
            $.each(data, (key, value) => {
                var row = $('<tr>');
                $('<td>').text(key).appendTo(row);

                if (JsonUtil.isSimpleType(value)) {
                    $('<td>').text(value).appendTo(row);
                } else if ($.type(value) === 'array') {
                    var col = $('<td>');
                    if (value.length > 0) { 
                        if (JsonUtil.isSimpleType(value[0])) {
                            var list = $('<ul />');
                            jQuery.each(value, (k, v) => list.append('<li>' + v + '</li>'));
                            list.appendTo(col);
                        } else {
                            JsonUtil.createTableRecursive(value).appendTo(col);
                        }
                    }

                    col.appendTo(row);
                } else if ($.type(value) === 'object') {
                    var col = $('<td>');
                    if (!jQuery.isEmptyObject(value))
                        JsonUtil.createTableRecursive(value).appendTo(col);
                    
                    col.appendTo(row);
                } else { 
                    console.log($.type(value));
                }

                table.append(row);
            });

            table.append('</tbody></table>');

            return table;
        }

        private static isSimpleType(value: any): boolean {
            return $.type(value) === 'string' || $.type(value) === 'number' || $.type(value) === 'boolean' || $.type(value) === 'null';
        }
    }
}