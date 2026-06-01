import { describe, expect, it } from 'vitest';

import { getOrganizationBillingInformation, normalizeOrganizationBillingInformationValue, organizationBillingInformationDataKeys } from './billing-information';

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
