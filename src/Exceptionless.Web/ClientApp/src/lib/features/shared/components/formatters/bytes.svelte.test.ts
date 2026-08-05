import { render, screen } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';

import Bytes from './bytes.svelte';

describe('Bytes', () => {
    it('uses readable units instead of locale-specific compact byte notation', () => {
        vi.spyOn(Navigator.prototype, 'language', 'get').mockReturnValue('en-GB');

        render(Bytes, { value: 25_000_000_000 });
        render(Bytes, { value: 17_000_000_000 });
        render(Bytes, { value: 156_000_000 });

        expect(screen.getByText('25 GB')).toBeTruthy();
        expect(screen.getByText('17 GB')).toBeTruthy();
        expect(screen.getByText('156 MB')).toBeTruthy();
    });

    it('formats event memory values with a readable GB unit', () => {
        render(Bytes, { value: 17_179_869_184 });

        expect(screen.getByText('17 GB')).toBeTruthy();
    });

    it('formats process memory with a readable MB unit', () => {
        render(Bytes, { value: 202_899_456 });

        expect(screen.getByText('203 MB')).toBeTruthy();
    });
});
