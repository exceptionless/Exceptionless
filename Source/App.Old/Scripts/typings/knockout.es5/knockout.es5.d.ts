// Type definitions for Knockout ES5
// Project: https://github.com/SteveSanderson/knockout-es5/blob/master/src/knockout-es5.js
// Definitions by: Blake Niemyjski <https://github.com/niemyjski/>
// Definitions: https://github.com/borisyankov/DefinitelyTyped

/// <reference path="../knockout/knockout.d.ts" />

interface KnockoutStatic {
    track(obj: any, propertyNames?: string[]): any;
    defineProperty(obj: any, propertyName: string, evaluatorOrOptions?: any): any;
    getObservable(obj: any, propertyName: string): any;
}
