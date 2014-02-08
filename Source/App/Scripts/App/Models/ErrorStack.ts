/// <reference path="../exceptionless.ts" />

module exceptionless.models {
    export class ErrorStack extends ErrorBase {
        title = ko.observable<string>('');
        totalOccurrences = ko.observable<number>(0);
        firstOccurrence = ko.observable<Date>(DateUtil.minValue.toDate());
        lastOccurrence = ko.observable<Date>(DateUtil.minValue.toDate());

        constructor(id: string, typeFullName: string, methodFullName: string, path: string, is404: boolean, title: string, totalOccurrences: number, firstOccurrence: Date, lastOccurrence: Date) {
            super(id, typeFullName, methodFullName, path, is404);

            this.title(title);
            this.totalOccurrences(totalOccurrences);
            this.firstOccurrence(firstOccurrence);
            this.lastOccurrence(lastOccurrence);
        }
    }
}