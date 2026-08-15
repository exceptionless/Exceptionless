import { describe, expect, it } from 'vitest';

import { getSessionColumns } from './session-table-columns';

describe('getSessionColumns', () => {
    it('marks Summary as the flexible full-width column', () => {
        const summaryColumn = getSessionColumns().find((column) => column.id === 'summary');

        expect(summaryColumn).toMatchObject({
            header: 'Summary',
            meta: {
                class: 'w-full'
            }
        });
    });
});
