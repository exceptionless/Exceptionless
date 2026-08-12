import { render } from '@testing-library/svelte';
import { describe, expect, it } from 'vitest';

import ObjectDump from './object-dump.svelte';

describe('ObjectDump', () => {
    it('preserves line breaks in string values', () => {
        const sessionContents = 'ApplicationName = ZZZZ\r\nGL_StatusFilter_D = Active\r\nSessionID = abc123';

        const { container } = render(ObjectDump, { value: sessionContents });

        const value = container.firstElementChild;

        expect(value?.textContent).toBe(sessionContents);
        expect(value?.classList.contains('whitespace-pre-wrap')).toBe(true);
    });
});
