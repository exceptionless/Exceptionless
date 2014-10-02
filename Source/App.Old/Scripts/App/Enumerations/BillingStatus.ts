/// <reference path="../exceptionless.ts" />

module exceptionless.enumerations {
    export enum BillingStatus {
        Trialing = 0,
        Active = 1,
        PastDue = 2,
        Canceled = 3,
        Unpaid = 4
    }
}