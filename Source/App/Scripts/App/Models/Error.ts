/// <reference path="../exceptionless.ts" />

module exceptionless.models {
    export class Error extends ErrorBase {
        message = ko.observable<string>('');
        occurrence = ko.observable<Moment>(DateUtil.minValue);

        constructor(id: string, typeFullName: string, methodFullName: string, path: string, is404: boolean, message: string, occurrence: Date) {
            super(id, typeFullName, methodFullName, path, is404);

            this.message(message);
            this.occurrence(moment(occurrence));
        }
    }
}