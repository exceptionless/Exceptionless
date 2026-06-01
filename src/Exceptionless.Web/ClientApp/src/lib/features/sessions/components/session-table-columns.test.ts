import { describe, expect, it } from 'vitest';

import { getSessionColumns } from './session-table-columns';

describe('getSessionColumns', () => {
    it('keeps duration column sortable', () => {
        const result = getSessionColumns();
        const columnsById = Object.fromEntries(result.map((column) => [column.id, column]));

        expect(columnsById.duration?.enableSorting).toBeUndefined();
    });
});
