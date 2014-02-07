/// <reference path="../exceptionless.ts" />

module exceptionless.models {
    export class Parameter {
        name = '';
        type = '';
        isIn = false;
        isOut = false;
        isOptional = false;
        genericArguments: string[] = [];
        extendedData: models.KeyValuePair[] = [];

        constructor (name: string, type: string, genericArguments?: string[], extendedData?: any) {
            this.name = name;
            this.type = type;
            this.genericArguments = genericArguments;

            if (extendedData) {
                for (var field in extendedData) {
                    switch (field) {
                        case 'IsIn':
                            this.isIn = extendedData[field];
                            break;
                        case 'IsOut':
                            this.isOut = extendedData[field];
                            break;
                        case 'IsOptional':
                            this.isOptional = extendedData[field];
                            break;
                        default:
                            this.extendedData.push(new models.KeyValuePair(field, extendedData[field]));
                    }
                }
            }
        }

        public get isGeneric(): boolean {
            return !this.genericArguments && this.genericArguments.length > 0;
        }
    }
}