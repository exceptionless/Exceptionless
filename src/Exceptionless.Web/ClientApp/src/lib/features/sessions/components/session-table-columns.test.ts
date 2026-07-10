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

    it('allocates more width to User than the compact Duration column', () => {
        const durationColumn = getSessionColumns().find((column) => column.id === 'duration');
        const userColumn = getSessionColumns().find((column) => column.id === 'user');

        expect(durationColumn?.meta).toMatchObject({ class: 'w-40' });
        expect(userColumn?.meta).toMatchObject({ class: 'w-56' });
    });
});
