import { describe, expect, it } from 'vitest';

import { getInvoiceStatusLabel } from './invoice';

describe('getInvoiceStatusLabel', () => {
    it('describes a negative draft as pending credit', () => {
        expect(getInvoiceStatusLabel('draft', -12.5)).toBe('Pending credit');
    });

    it('describes a finalized negative invoice as issued credit', () => {
        expect(getInvoiceStatusLabel('paid', -12.5)).toBe('Credit issued');
    });

    it('describes an open positive invoice as payment due', () => {
        expect(getInvoiceStatusLabel('open', 12.5)).toBe('Payment due');
    });
});
