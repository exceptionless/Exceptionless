/// <reference path="../exceptionless.ts" />

module exceptionless.models {
    export class Invoice {
        id: string;
        date: Date;
        paid: boolean;

        constructor(id: string, date: Date, paid: boolean) {
            this.id = id;
            this.date = date;
            this.paid = paid;

            ko.track(this);
        }
    }
}