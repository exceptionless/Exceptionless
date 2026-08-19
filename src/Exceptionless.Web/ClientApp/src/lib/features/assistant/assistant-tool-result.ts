export function assistantToolErrorMessage(result: string | undefined): string | undefined {
    if (!result) {
        return;
    }

    try {
        const root = asRecord(JSON.parse(result));
        const error = asRecord(root?.error);
        return readString(error, 'message');
    } catch {
        return;
    }
}

export function assistantToolResultFailed(result: string | undefined): boolean {
    if (!result) {
        return false;
    }

    try {
        const value = JSON.parse(result) as unknown;
        return typeof value === 'object' && value !== null && 'ok' in value && value.ok === false;
    } catch {
        return false;
    }
}

export function formatAssistantToolJson(value: string | undefined): string {
    if (!value) {
        return '';
    }

    try {
        return JSON.stringify(JSON.parse(value), undefined, 2);
    } catch {
        return value;
    }
}

function asRecord(value: unknown): Record<string, unknown> | undefined {
    return typeof value === 'object' && value !== null && !Array.isArray(value) ? (value as Record<string, unknown>) : undefined;
}

function readString(record: Record<string, unknown> | undefined, ...keys: string[]): string | undefined {
    for (const key of keys) {
        if (typeof record?.[key] === 'string') {
            return record[key];
        }
    }
}
