/// <reference path="../exceptionless.ts" />

module exceptionless {
    export class DataUtil {
        constructor() { }
        
        public static getProjectId(): string {
            var pathName = location.pathname;
            // HACK: force IE9 to use isolated storage if the hash contains history state.
            if (location.hash.length > 0) {
                var hashes = location.hash.split('#');
                if (hashes.length > 0 && hashes[1].indexOf(Constants.PROJECT) > 0) {
                    pathName = '';
                }
            }

            var projectId: string;
            var paths = pathName.split('/');
            if (paths.length >= 3 && paths[1] === Constants.PROJECT && paths[2].length === 24)
                projectId = paths[2];
            else
                projectId = DataUtil.getValue(Constants.PROJECT);

            return projectId;
        }

        public static getOrganizationId(): string {
            var pathName = location.pathname;
            // HACK: force IE9 to use isolated storage if the hash contains history state.
            if (location.hash.length > 0) {
                var hashes = location.hash.split('#');
                if (hashes.length > 0 && hashes[1].indexOf(Constants.ORGANIZATION) > 0) {
                    pathName = '';
                }
            }

            var organizationId: string;
            var paths = pathName.split('/');
            if (paths.length >= 3 && paths[1] === Constants.ORGANIZATION && paths[2].length === 24)
                organizationId = paths[2];
            else
                organizationId = DataUtil.getValue(Constants.ORGANIZATION);

            return organizationId;
        }

        public static getQueryStringValue(key: string): string {
            if (StringUtil.isNullOrEmpty(key))
                return null;

            var query = location.search.substring(1);
            var vars = query.split('&');
            for (var i = 0; i < vars.length; i++) {
                var pair = vars[i].split('=');
                if (decodeURIComponent(pair[0]) === key) {
                    return decodeURIComponent(pair[1]);
                }
            }

            return null;
        }

        public static updateQueryStringParameter(uri, key, value): string {
            if (StringUtil.isNullOrEmpty(key))
                return uri;

            var regex = new RegExp("([?|&])" + encodeURIComponent(key) + "=.*?(&|$)", "i");
            if (uri.match(regex)) {
                if (!StringUtil.isNullOrEmpty(value)) {
                    uri = uri.replace(regex, '$1' + encodeURIComponent(key) + "=" + encodeURIComponent(value) + '$2');
                } else {
                    var parts = uri.split('?');
                    if (parts.length >= 2) {
                        var uriBasePath = parts.shift();
                        var queryString = parts.join("?");

                        var prefix = encodeURIComponent(key) + '=';
                        var pars = queryString.split(/[&;]/g);
                        for (var i = pars.length; i-- > 0;) {
                            if (pars[i].lastIndexOf(prefix, 0) !== -1)
                                pars.splice(i, 1);
                        }

                        uri = uriBasePath + '?' + pars.join('&');
                    }
                }
            } else if (!StringUtil.isNullOrEmpty(value)) {
                var separator = uri.indexOf('?') !== -1 ? "&" : "?";
                uri += separator + encodeURIComponent(key) + "=" + encodeURIComponent(value);
            }

            return uri;
        }

        public static getValue(key: string): any { 
            var val = this.getQueryStringValue(key);
            if (!StringUtil.isNullOrEmpty(val))
                return val;

            return localStorage.getItem(key);
        }

        public static serializeObject(element: JQuery, ignoredIds?: string[]): any {
            var result = {};

            if (!ignoredIds)
                ignoredIds = ['__RequestVerificationToken'];

            $.each(element.serializeArray(), (index: number, element: any) => {
                if (ignoredIds.indexOf(element.name) > -1)
                    return;

                if (result[element.name] !== undefined) {
                    if (!result[element.name].push) {
                        result[element.name] = [result[element.name]];
                    }
                    result[element.name].push(element.value || '');
                } else {
                    result[element.name] = element.value || '';
                }
            });

            return result;
        }

        static submitForm(form: JQuery, success?: string, error?: string): void;
        static submitForm(form: JQuery, success?: (data: any) => void , error?: string): void;
        static submitForm(form: JQuery, success?: (data: any) => void , error?: (jqXHR: JQueryXHR, status: string, errorThrown: string) => void ): void;

        public static submitForm(form: JQuery, success?: any, error?: any): void {
            var headers: { [key: string]: any; } = {};

            var token = form.find('input[name="__RequestVerificationToken"] :first');
            if (token.length > 0)
                headers['__RequestVerificationToken'] = token.val();

            $.ajax(form.attr('action'), {
                type: 'POST',
                contentType: 'application/json;charset=utf-8',
                data: JSON.stringify(DataUtil.serializeObject(form)),
                headers: headers,
                statusCode: {
                    400: (jqXHR: JQueryXHR, status: string, errorThrown: string) => {
                        try {
                            var errors = {};
                            $.each(JSON.parse(jqXHR.responseText), (name: string, value: string) => {
                                if (StringUtil.isNullOrEmpty(name))
                                    App.showErrorNotification('The following error occurred while processing your request: ' + value);
                                else
                                    errors[name] = value;
                            });

                            form.validate().showErrors(errors);
                        } catch (ex) {
                            App.showErrorNotification('One or more validation errors occurred. Please ensure the form is valid and try again.');
                            console.log(ex);
                        }
                    }
                },
                success: (data: any) => {
                    if (!success)
                        return;

                    if (success instanceof Function)
                        success(data);
                    else
                        App.showSuccessNotification(success);
                },
                error: (jqXHR: JQueryXHR, status: string, errorThrown: string) => {
                    if (!error)
                        return;

                    if (error instanceof Function)
                        error(jqXHR, status, errorThrown);
                    else
                        App.showErrorNotification(error);
                }
            });
        }
    }
}