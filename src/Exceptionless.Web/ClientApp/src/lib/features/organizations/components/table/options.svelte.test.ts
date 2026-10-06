import type { ViewOrganization } from '$features/organizations/models';

import { cleanup, fireEvent, render, screen } from '@testing-library/svelte';
import { afterEach, describe, expect, it, vi } from 'vitest';

import OrganizationOrderingTestHarness from './organization-ordering.test-harness.svelte';

vi.mock('$env/dynamic/public', () => ({ env: {} }));

afterEach(cleanup);

const createOrganizations = () => Array.from({ length: 21 }, (_, index) => ({ id: String(index), name: `Organization ${21 - index}` }) as ViewOrganization);
const names = () => screen.getAllByRole('listitem').map((item) => item.textContent);

describe('organization table ordering', () => {
    it('OrganizationTable_WithMoreThanOnePage_SortsBeforePagingAndHonorsDirection', async () => {
        // Arrange
        const organizations = createOrganizations();
        const originalNames = organizations.map((organization) => organization.name);
        render(OrganizationOrderingTestHarness, { organizations });

        // Act / Assert: the item received last belongs on the first page.
        expect(names()).toEqual(Array.from({ length: 20 }, (_, index) => `Organization ${index + 1}`));
        expect(screen.getByLabelText('Page count')).toHaveTextContent('2');
        await fireEvent.click(screen.getByRole('button', { name: 'Next page' }));
        expect(names()).toEqual(['Organization 21']);
        expect(screen.getByLabelText('Page')).toHaveTextContent('1');
        expect(screen.getByLabelText('Parameter page')).toHaveTextContent('2');

        await fireEvent.click(screen.getByRole('button', { name: 'Sort descending' }));
        expect(screen.getByLabelText('Page')).toHaveTextContent('0');
        expect(names()).toEqual(Array.from({ length: 20 }, (_, index) => `Organization ${21 - index}`));
        await fireEvent.click(screen.getByRole('button', { name: 'Next page' }));
        expect(names()).toEqual(['Organization 1']);

        await fireEvent.click(screen.getByRole('button', { name: 'Sort ascending' }));
        expect(names()[0]).toBe('Organization 1');
        await fireEvent.click(screen.getByRole('button', { name: 'Five rows' }));
        expect(names()).toHaveLength(5);
        expect(screen.getByLabelText('Page count')).toHaveTextContent('5');
        expect(organizations.map((organization) => organization.name)).toEqual(originalNames);
    });

    it('OrganizationTable_WhenResultsChange_ResortsAndClampsTheCurrentPage', async () => {
        // Arrange
        const organizations = createOrganizations();
        const { rerender } = render(OrganizationOrderingTestHarness, { organizations });
        await fireEvent.click(screen.getByRole('button', { name: 'Next page' }));

        await fireEvent.click(screen.getByRole('button', { name: 'Select row' }));

        // Act
        await rerender({ organizations: [{ ...organizations[0]!, name: 'AAA Renamed Organization' }] });

        // Assert
        expect(names()).toEqual(['AAA Renamed Organization']);
        expect(screen.getByLabelText('Selected rows')).toHaveTextContent('0');
        expect(screen.getByLabelText('Page')).toHaveTextContent('0');
        expect(screen.getByLabelText('Page count')).toHaveTextContent('1');

        // Act
        await rerender({ organizations: [...organizations, { ...organizations[0]!, id: 'new', name: 'AAA Created Organization' }] });

        // Assert
        expect(names()[0]).toBe('AAA Created Organization');
        expect(screen.getByLabelText('Page count')).toHaveTextContent('2');
    });
});
