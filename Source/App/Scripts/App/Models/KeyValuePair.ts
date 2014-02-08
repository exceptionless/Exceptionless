/// <reference path="../exceptionless.ts" />

module exceptionless.models {
    export class KeyValuePair {
        private static _uniqueId: number = 0;
        id = ko.observable(++KeyValuePair._uniqueId);
        key = ko.observable<string>('').extend({ required: true });
        value = ko.observable<string>('').extend({ required: true });

        constructor(key: string, value: string) {
            this.key(key);
            this.value(value);
        }

        public get isValid(): Boolean {
            return this.key.isValid() && this.value.isValid();
        }
    }
}