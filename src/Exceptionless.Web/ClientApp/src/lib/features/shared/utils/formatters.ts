export function formatCurrency(value: number, currency = 'USD', locale: Intl.LocalesArgument = 'en-US'): string {
    return new Intl.NumberFormat(locale, { currency, maximumFractionDigits: 0, style: 'currency' }).format(value);
}
