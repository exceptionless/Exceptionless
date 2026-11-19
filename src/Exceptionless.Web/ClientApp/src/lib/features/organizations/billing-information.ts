import type { ViewOrganization } from './models';
import type { OrganizationBillingInformationFormData } from './schemas';

export const organizationBillingInformationDataKeys = {
    address: 'billing_address',
    name: 'billing_name',
    vatId: 'billing_vat_id',
    vatNumber: 'billing_vat_number'
} as const;

export interface OrganizationBillingInformationChange {
    key: (typeof organizationBillingInformationDataKeys)[keyof typeof organizationBillingInformationDataKeys];
    value: null | string;
}

export interface OrganizationBillingInformationWriter {
    remove: (key: OrganizationBillingInformationChange['key']) => Promise<unknown>;
    set: (key: OrganizationBillingInformationChange['key'], value: string) => Promise<unknown>;
}

export function createSerializedBillingInformationSave(save: (organizationId: string) => Promise<void>) {
    let pendingSave = Promise.resolve();

    return (organizationId: string): Promise<void> => {
        const nextSave = pendingSave.then(() => save(organizationId));
        pendingSave = nextSave.catch(() => undefined);
        return nextSave;
    };
}

export function getOrganizationBillingInformation(organization?: null | Pick<ViewOrganization, 'data'>): OrganizationBillingInformationFormData {
    const data = organization?.data;

    return {
        address: getOrganizationBillingInformationValue(data?.[organizationBillingInformationDataKeys.address]),
        name: getOrganizationBillingInformationValue(data?.[organizationBillingInformationDataKeys.name]),
        vatId: getOrganizationBillingInformationValue(data?.[organizationBillingInformationDataKeys.vatId]),
        vatNumber: getOrganizationBillingInformationValue(data?.[organizationBillingInformationDataKeys.vatNumber])
    };
}

export function getOrganizationBillingInformationChanges(
    current: OrganizationBillingInformationFormData,
    next: OrganizationBillingInformationFormData
): OrganizationBillingInformationChange[] {
    return (Object.keys(organizationBillingInformationDataKeys) as (keyof OrganizationBillingInformationFormData)[]).flatMap((field) => {
        const currentValue = normalizeOrganizationBillingInformationValue(current[field]);
        const nextValue = normalizeOrganizationBillingInformationValue(next[field]);

        return currentValue === nextValue ? [] : [{ key: organizationBillingInformationDataKeys[field], value: nextValue }];
    });
}

export function normalizeOrganizationBillingInformationValue(value: string): null | string {
    const trimmedValue = value.trim();
    return trimmedValue || null;
}

export async function saveOrganizationBillingInformationChanges(
    changes: OrganizationBillingInformationChange[],
    writer: OrganizationBillingInformationWriter
): Promise<void> {
    for (const change of changes) {
        if (change.value === null) {
            await writer.remove(change.key);
        } else {
            await writer.set(change.key, change.value);
        }
    }
}

function getOrganizationBillingInformationValue(value: unknown): string {
    return typeof value === 'string' ? value : '';
}
