import { describe, expect, it, vi } from 'vitest';

import {
    createSerializedBillingInformationSave,
    getOrganizationBillingInformation,
    getOrganizationBillingInformationChanges,
    normalizeOrganizationBillingInformationValue,
    organizationBillingInformationDataKeys,
    saveOrganizationBillingInformationChanges
} from './billing-information';

describe('getOrganizationBillingInformation', () => {
    it('returns billing information from known organization data keys', () => {
        // Arrange
        const organization = {
            data: {
                [organizationBillingInformationDataKeys.address]: '123 Main Street',
                [organizationBillingInformationDataKeys.name]: 'Acme, Inc.',
                [organizationBillingInformationDataKeys.vatId]: 'DE123456789',
                [organizationBillingInformationDataKeys.vatNumber]: '123456789'
            }
        };

        // Act
        const billingInformation = getOrganizationBillingInformation(organization);

        // Assert
        expect(billingInformation).toEqual({
            address: '123 Main Street',
            name: 'Acme, Inc.',
            vatId: 'DE123456789',
            vatNumber: '123456789'
        });
    });

    it('defaults missing or non-string billing information values to empty strings', () => {
        // Arrange
        const organization = {
            data: {
                [organizationBillingInformationDataKeys.address]: ['invalid'],
                [organizationBillingInformationDataKeys.name]: null,
                [organizationBillingInformationDataKeys.vatId]: undefined,
                [organizationBillingInformationDataKeys.vatNumber]: 42
            }
        };

        // Act
        const billingInformation = getOrganizationBillingInformation(organization);

        // Assert
        expect(billingInformation).toEqual({
            address: '',
            name: '',
            vatId: '',
            vatNumber: ''
        });
    });
});

describe('normalizeOrganizationBillingInformationValue', () => {
    it('trims non-empty values and removes blank values', () => {
        // Arrange
        const value = '  DE123456789  ';
        const blankValue = '   ';

        // Act
        const normalizedValue = normalizeOrganizationBillingInformationValue(value);
        const normalizedBlankValue = normalizeOrganizationBillingInformationValue(blankValue);

        // Assert
        expect(normalizedValue).toBe('DE123456789');
        expect(normalizedBlankValue).toBeNull();
    });
});

describe('getOrganizationBillingInformationChanges', () => {
    it('returns only normalized values that changed', () => {
        const current = {
            address: '123 Main Street',
            name: 'Acme, Inc.',
            vatId: 'DE123456789',
            vatNumber: ''
        };

        const changes = getOrganizationBillingInformationChanges(current, {
            ...current,
            name: '  Acme, Inc.  ',
            vatId: '  ',
            vatNumber: '  123456789  '
        });

        expect(changes).toEqual([
            { key: organizationBillingInformationDataKeys.vatId, value: null },
            { key: organizationBillingInformationDataKeys.vatNumber, value: '123456789' }
        ]);
    });
});

describe('saveOrganizationBillingInformationChanges', () => {
    it('waits for each organization write before starting the next one', async () => {
        const firstWrite = Promise.withResolvers<void>();
        const calls: string[] = [];
        const writer = {
            remove: vi.fn(async (key: string) => {
                calls.push(`remove:${key}`);
            }),
            set: vi.fn(async (key: string, value: string) => {
                calls.push(`set:${key}:${value}`);
                await firstWrite.promise;
            })
        };

        const save = saveOrganizationBillingInformationChanges(
            [
                { key: organizationBillingInformationDataKeys.name, value: 'Acme, Inc.' },
                { key: organizationBillingInformationDataKeys.vatId, value: null }
            ],
            writer
        );

        expect(calls).toEqual([`set:${organizationBillingInformationDataKeys.name}:Acme, Inc.`]);
        expect(writer.remove).not.toHaveBeenCalled();

        firstWrite.resolve();
        await save;

        expect(calls).toEqual([`set:${organizationBillingInformationDataKeys.name}:Acme, Inc.`, `remove:${organizationBillingInformationDataKeys.vatId}`]);
    });
});

describe('createSerializedBillingInformationSave', () => {
    it('serializes overlapping autosaves and continues after a rejected save', async () => {
        const firstSave = Promise.withResolvers<void>();
        const calls: string[] = [];
        let activeSaves = 0;
        let maximumActiveSaves = 0;
        const save = vi.fn(async (organizationId: string) => {
            calls.push(organizationId);
            activeSaves++;
            maximumActiveSaves = Math.max(maximumActiveSaves, activeSaves);

            try {
                if (organizationId === 'first') {
                    await firstSave.promise;
                    throw new Error('save failed');
                }
            } finally {
                activeSaves--;
            }
        });
        const serializedSave = createSerializedBillingInformationSave(save);

        const first = serializedSave('first');
        const second = serializedSave('second');

        await vi.waitFor(() => expect(calls).toEqual(['first']));
        firstSave.resolve();
        await expect(first).rejects.toThrow('save failed');
        await second;

        expect(calls).toEqual(['first', 'second']);
        expect(maximumActiveSaves).toBe(1);
    });
});
