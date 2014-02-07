/// <reference path="../exceptionless.ts" />

module exceptionless.models {
    export class StackFrame {
        name = '';
        declaringType = '';
        fileName = '';
        lineNumber = 0;
        columnNumber = 0;
        iLOffset = 0;
        nativeOffset = 0;
        attributes = 0;
        genericArguments: string[] = [];
        parameters: Parameter[] = [];
        extendedData: models.KeyValuePair[] = [];

        constructor (name: string, declaringType: string, fileName?: string, lineNumber?: number, columnNumber?: number, genericArguments?: string[], extendedData?: any, parameters?: Parameter[]) {
            this.name = name;
            this.declaringType = declaringType;
            this.fileName = fileName;
            this.lineNumber = lineNumber;
            this.columnNumber = columnNumber;
            this.genericArguments = genericArguments;
            this.parameters = parameters;

            if (extendedData) {
                for (var field in extendedData) {
                    switch (field) {
                        case 'ILOffset':
                            this.iLOffset = extendedData[field];
                            break;
                        case 'NativeOffset':
                            this.nativeOffset = extendedData[field];
                            break;
                        case 'Attributes':
                            this.attributes = extendedData[field];
                            break;
                        default:
                            this.extendedData.push(new models.KeyValuePair(field, extendedData[field]));
                    }
                }
            }
        }

        public get hasFileName(): boolean {
            return !StringUtil.isNullOrEmpty(this.fileName);
        }

        public get hasLineNumber(): boolean {
            return this.lineNumber > 0;
        }

        public get hasColumnNumber(): boolean {
            return this.columnNumber > 0;
        }

        public get isGeneric(): boolean {
            return this.genericArguments.length > 0;
        }

        public get hasParameters(): boolean {
            return this.parameters.length > 0;
        }
    }
}