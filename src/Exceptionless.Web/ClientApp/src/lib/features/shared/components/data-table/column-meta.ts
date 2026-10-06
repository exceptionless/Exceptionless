export interface DataTableColumnMeta {
    class?: string;
    enableWrapping?: boolean;
}

export function getDataTableColumnMeta(meta: unknown): DataTableColumnMeta {
    return (meta ?? {}) as DataTableColumnMeta;
}

export function supportsColumnWrapping(meta: unknown): boolean {
    return getDataTableColumnMeta(meta).enableWrapping === true;
}
