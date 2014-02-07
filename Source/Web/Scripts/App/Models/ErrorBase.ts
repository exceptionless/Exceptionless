/// <reference path="../exceptionless.ts" />

module exceptionless.models {
    export class ErrorBase {
        id = ko.observable<string>('');
        type = ko.observable<string>('');
        typeFullName = ko.observable<string>('');
        method = ko.observable<string>('');
        methodFullName = ko.observable<string>('');
        path = ko.observable<string>('');
        is404 = ko.observable<boolean>(false);

        constructor(id: string, typeFullName: string, methodFullName: string, path: string, is404: boolean) {
            this.id(id);
            this.typeFullName(typeFullName);
            this.methodFullName(methodFullName);
            this.path(path);
            this.is404(is404);

            // TODO: Rework this so it's generic and works with all languages...
            if (!StringUtil.isNullOrEmpty(this.typeFullName())) {
                var parts = this.typeFullName().split('.');
                this.type(parts.length > 0 ? parts[parts.length - 1] : this.typeFullName());
            }

            if (!StringUtil.isNullOrEmpty(this.methodFullName())) {
                var parts = this.methodFullName().match(/([\w\<\>\[\]]+)\(/);
                if (!parts)
                    var parts = this.methodFullName().split('.');

                this.method(parts && parts.length > 0 ? parts[parts.length - 1] : this.methodFullName());
            }
        }


        public get hasMethod(): KnockoutComputed<boolean> {
            return ko.computed(() => {
                return !StringUtil.isNullOrEmpty(this.method());
            }, this);
        }

        public get hasPath(): KnockoutComputed<boolean> {
            return ko.computed(() => {
                return !StringUtil.isNullOrEmpty(this.path());
            }, this);
        }
    }
}