interface JQuery {
    payment(validatorName: string);
}

interface JQueryStatic {
    payment: JQueryPayment;
}

interface JQueryPayment {
    validateCardNumber(cardNumber: string) : boolean;
    validateCardExpiry(year: string, month: string) : boolean;
    validateCardExpiry(expiry: any) : boolean;
    validateCardCVC(cvc: string, type: string) : boolean;
    cardType(cardNumber: string): string;
    cardExpiryVal(monthYear: string): JQueryPaymentExpiryInfo;
}

interface JQueryPaymentExpiryInfo {
    month: number;
    year: number;
}