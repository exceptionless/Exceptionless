import type { ViewOrganization } from './models';

export const organizationBillingInformationDataKeys = {
    address: 'billing_address',
    name: 'billing_name',
    vatId: 'billing_vat_id',
    vatNumber: 'billing_vat_number'
} as const;

export interface OrganizationBillingInformation {
    address: string;
    name: string;
    vatId: string;
    vatNumber: string;
}

export interface OrganizationBillingInformationChange {
    key: (typeof organizationBillingInformationDataKeys)[keyof typeof organizationBillingInformationDataKeys];
    value: null | string;
}

export function getOrganizationBillingInformation(organization?: null | Pick<ViewOrganization, 'data'>): OrganizationBillingInformation {
    const data = organization?.data;

    return {
        address: getOrganizationBillingInformationValue(data?.[organizationBillingInformationDataKeys.address]),
        name: getOrganizationBillingInformationValue(data?.[organizationBillingInformationDataKeys.name]),
        vatId: getOrganizationBillingInformationValue(data?.[organizationBillingInformationDataKeys.vatId]),
        vatNumber: getOrganizationBillingInformationValue(data?.[organizationBillingInformationDataKeys.vatNumber])
    };
}

export function getOrganizationBillingInformationChanges(
    current: OrganizationBillingInformation,
    next: OrganizationBillingInformation
): OrganizationBillingInformationChange[] {
    return (Object.keys(organizationBillingInformationDataKeys) as (keyof OrganizationBillingInformation)[]).flatMap((field) => {
        const currentValue = normalizeOrganizationBillingInformationValue(current[field]);
        const nextValue = normalizeOrganizationBillingInformationValue(next[field]);

        return currentValue === nextValue ? [] : [{ key: organizationBillingInformationDataKeys[field], value: nextValue }];
    });
}

export function normalizeOrganizationBillingInformationValue(value: string): null | string {
    const trimmedValue = value.trim();
    return trimmedValue || null;
}

function getOrganizationBillingInformationValue(value: unknown): string {
    return typeof value === 'string' ? value : '';
}
