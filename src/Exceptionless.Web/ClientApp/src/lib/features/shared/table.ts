import { type Table as SvelteTable } from '@tanstack/svelte-table';

export function isTableEmpty<TData>(table: SvelteTable<TData>): boolean {
    return table.options.data.length === 0;
}

/***
 * Removes data from the table.
 * @param table The table to remove data from.
 * @param predicate A function that determines whether a row should be removed.
 * @returns True if data was removed, false otherwise.
 */
export function removeTableData<TData>(table: SvelteTable<TData>, predicate: (value: TData, index: number, array: TData[]) => boolean): boolean {
    if (table.options.data.some(predicate)) {
        table.options.data = table.options.data.filter((value, index, array) => !predicate(value, index, array));
        return true;
    }

    return false;
}

/***
 * Removes a selection from the table.
 * @param table The table to remove the selection from.
 * @param selectionId The id of the selection to remove.
 * @returns True if the selection was removed, false otherwise.
 */
export function removeTableSelection<TData>(table: SvelteTable<TData>, selectionId: string): boolean {
    if (table.getIsSomeRowsSelected()) {
        const { rowSelection } = table.getState();
        if (rowSelection[selectionId]) {
            table.setRowSelection((old: Record<string, boolean>) => {
                const filtered = Object.entries(old).filter(([id]) => id !== selectionId);
                return Object.fromEntries(filtered);
            });

            return true;
        }
    }

    return false;
}
