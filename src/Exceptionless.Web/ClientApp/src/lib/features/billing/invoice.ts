export function getInvoiceStatusLabel(status: string, total: number) {
    if (status === 'draft' && total < 0) {
        return 'Pending credit';
    }

    if (status === 'paid' && total < 0) {
        return 'Credit issued';
    }

    switch (status) {
        case 'draft':
            return 'Draft';
        case 'open':
            return 'Payment due';
        case 'paid':
            return 'Paid';
        case 'uncollectible':
            return 'Uncollectible';
        case 'void':
            return 'Void';
        default:
            return status;
    }
}
