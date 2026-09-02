import { fireEvent, render, screen } from '@testing-library/svelte';
import { describe, expect, it } from 'vitest';

import ProductTourPeriod from './product-tour-period.svelte';

describe('ProductTourPeriod', () => {
    it('edits the selected month inside the popover rather than adding a toolbar input', async () => {
        // Arrange
        render(ProductTourPeriod, { range: { kind: 'month', month: '2020-08' } });
        expect(screen.queryByLabelText('Month (UTC)')).toBeNull();

        // Act
        await fireEvent.click(screen.getByRole('button', { name: 'Usage period: August 2020' }));

        // Assert
        expect((screen.getByLabelText('Month (UTC)') as HTMLInputElement).value).toBe('2020-08');
    });

    it('switches history back to the remembered month using the same trigger', async () => {
        // Arrange
        render(ProductTourPeriod, { range: { kind: 'month', month: '2020-08' } });
        await fireEvent.click(screen.getByRole('button', { name: 'Usage period: August 2020' }));

        // Act
        await fireEvent.click(screen.getByRole('button', { name: 'Available history' }));
        await fireEvent.click(screen.getByRole('button', { name: 'Usage period: Available history' }));
        await fireEvent.click(screen.getByRole('button', { name: 'Show month' }));

        // Assert
        expect(screen.getByRole('button', { name: 'Usage period: August 2020' })).toBeTruthy();
        expect(screen.queryByLabelText('Month (UTC)')).toBeNull();
    });
});
