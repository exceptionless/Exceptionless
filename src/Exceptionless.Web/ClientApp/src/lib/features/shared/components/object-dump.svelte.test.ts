import { render } from '@testing-library/svelte';
import { describe, expect, it } from 'vitest';

import ObjectDump from './object-dump.svelte';

describe('ObjectDump', () => {
    it('preserves line breaks in string values', () => {
        const { container } = render(ObjectDump, { value: 'First line\r\nSecond line\r\nThird line' });

        const value = container.firstElementChild;

        expect(value?.textContent).toBe('First line\r\nSecond line\r\nThird line');
        expect(value?.classList.contains('whitespace-pre-wrap')).toBe(true);
    });
});
