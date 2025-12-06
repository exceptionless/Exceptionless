import { SuspensionCode } from '$features/organizations/models';

export function getSuspensionLabel(code: null | string | undefined): string {
    switch (code) {
        case 'Abuse':
            return 'Abuse Detected';
        case 'Billing':
            return 'Billing Issue';
        case 'Other':
            return 'Other';
        case 'Overage':
            return 'Over Limit';
        default:
            return 'Suspended';
    }
}

export function getSuspensionDescription(code: null | string | undefined, notes?: null | string): string {
    if (notes?.trim()) {
        return notes;
    }

    switch (code) {
        case 'Abuse':
            return 'This organization has been suspended due to abuse.';
        case 'Billing':
            return 'This organization has been suspended due to billing issues.';
        case 'Overage':
            return 'This organization has been suspended due to exceeding usage limits.';
        case 'Other':
        default:
            return 'This organization has been suspended.';
    }
}
