import type { ProductTourSummary } from '$generated/api';

import { ProductTourKind } from '$generated/api';
import { cleanup, render, screen } from '@testing-library/svelte';
import { afterEach, describe, expect, it } from 'vitest';

import ProductTourTotals from './product-tour-totals.svelte';

const emptyGuide: ProductTourSummary = {
    activity: [],
    completed: 0,
    dismissed: 0,
    kind: ProductTourKind.Guide,
    name: 'app-overview',
    shown: 0,
    start_sources: [],
    started: 0,
    version: 1
};

afterEach(cleanup);

describe('ProductTourTotals', () => {
    it('shows only meaningful guide counts without undefined percentage placeholders', () => {
        // Act
        render(ProductTourTotals, { tour: emptyGuide });

        // Assert
        expect(screen.getAllByText('0')).toHaveLength(3);
        expect(screen.queryByText('Shown')).toBeNull();
        expect(screen.queryByText('Recorded events')).toBeNull();
        expect(screen.queryByText(/of starts|—/)).toBeNull();
    });

    it('shows completion and dismissal percentages when starts exist', () => {
        // Arrange
        const tour = { ...emptyGuide, completed: 3, dismissed: 1, started: 4 };

        // Act
        render(ProductTourTotals, { tour });

        // Assert
        expect(screen.getByText('75.0% of starts')).toBeTruthy();
        expect(screen.getByText('25.0% of starts')).toBeTruthy();
    });

    it('uses invitations shown for invitation rates, without labeling acceptance as completion', () => {
        // Arrange
        const tour = { ...emptyGuide, completed: 3, dismissed: 7, kind: ProductTourKind.Prompt, shown: 10, started: 2 };

        // Act
        render(ProductTourTotals, { tour });

        // Assert
        expect(screen.getByText('Accepted')).toBeTruthy();
        expect(screen.queryByText('Completed')).toBeNull();
        expect(screen.getByText('20.0% of invitations shown')).toBeTruthy();
        expect(screen.getByText('30.0% of invitations shown')).toBeTruthy();
    });

    it('does not show undefined rates for invitations that have never been shown', () => {
        // Arrange
        const tour = { ...emptyGuide, kind: ProductTourKind.Prompt };

        // Act
        render(ProductTourTotals, { tour });

        // Assert
        expect(screen.getAllByText('0')).toHaveLength(4);
        expect(screen.queryByText(/of invitations shown|—/)).toBeNull();
    });
});
