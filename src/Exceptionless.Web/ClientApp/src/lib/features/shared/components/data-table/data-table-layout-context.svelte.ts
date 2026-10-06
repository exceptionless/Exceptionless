import { getContext, setContext } from 'svelte';

interface DataTableLayoutContext {
    getFillerColumnCount: () => number;
}

const dataTableLayoutContextKey = Symbol('data-table-layout');

export function getDataTableLayoutContext(): DataTableLayoutContext | undefined {
    return getContext<DataTableLayoutContext | undefined>(dataTableLayoutContextKey);
}

export function setDataTableLayoutContext(context: DataTableLayoutContext): void {
    setContext(dataTableLayoutContextKey, context);
}
