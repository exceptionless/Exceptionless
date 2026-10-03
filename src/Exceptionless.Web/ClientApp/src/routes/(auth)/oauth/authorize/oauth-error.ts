export function getOAuthErrorMessage(response: { data: unknown; problem: unknown }, fallback: string): string {
    for (const body of [response.data, response.problem]) {
        if (!body || typeof body !== 'object') {
            continue;
        }

        const error = body as Record<string, unknown>;
        for (const key of ['error_description', 'error', 'detail', 'title']) {
            const value = error[key];
            if (typeof value === 'string' && value.trim()) {
                return value;
            }
        }
    }

    return fallback;
}
