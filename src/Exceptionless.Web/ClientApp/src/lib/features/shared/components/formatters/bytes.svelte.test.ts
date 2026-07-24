import { render, screen } from '@testing-library/svelte';
import { describe, expect, it } from 'vitest';

import Bytes from './bytes.svelte';

describe('Bytes', () => {
    it('formats event memory values with a readable GB unit', () => {
        render(Bytes, { value: 17_179_869_184 });

        expect(screen.getByText('17 GB')).toBeTruthy();
    });

    it('formats process memory with a readable MB unit', () => {
        render(Bytes, { value: 202_899_456 });

        expect(screen.getByText('203 MB')).toBeTruthy();
    });
});
