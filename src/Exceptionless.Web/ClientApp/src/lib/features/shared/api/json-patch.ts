/**
 * RFC 6902 JSON Patch utilities.
 * Converts partial objects to JSON Patch operations for PATCH API calls.
 */

export interface JsonPatchOperation {
    op: 'replace' | 'test';
    path: string;
    value?: unknown;
}

/**
 * Converts a partial object into an array of RFC 6902 JSON Patch "replace" operations.
 * Each top-level property becomes a `replace` operation with a snake_case path.
 *
 * @example
 * toJsonPatch({ name: "New Name", deleteBotDataEnabled: true })
 * // => [{ op: "replace", path: "/name", value: "New Name" }, { op: "replace", path: "/delete_bot_data_enabled", value: true }]
 */
export function toJsonPatch(data: Record<string, unknown>): JsonPatchOperation[] {
    return Object.entries(data)
        .filter(([, value]) => value !== undefined)
        .map(([key, value]) => ({
            op: 'replace' as const,
            path: `/${key}`,
            value
        }));
}
