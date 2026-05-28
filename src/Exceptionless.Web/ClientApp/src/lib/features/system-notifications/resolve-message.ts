/**
 * Three-tier system notification message resolution.
 *
 * Priority (highest to lowest):
 * 1. realtimeMessage — from WebSocket (undefined = not yet received, null = explicitly cleared)
 * 2. persistedMessage — from GET /notifications/system on mount
 * 3. fallbackMessage — from PUBLIC_SYSTEM_NOTIFICATION_MESSAGE env var
 *
 * Important behaviors:
 * - When realtimeMessage is null (explicit WebSocket clear), the persisted value is skipped —
 *   only the env fallback can show. This means a realtime clear does NOT suppress the env
 *   fallback; operators who want a "clear everything" effect should also unset
 *   PUBLIC_SYSTEM_NOTIFICATION_MESSAGE or use the API clear endpoint.
 * - Empty strings are treated as absent (no notification to display).
 */
export function resolveDisplayMessage(
    realtimeMessage: null | string | undefined,
    persistedMessage: null | string,
    fallbackMessage: null | string
): null | string {
    if (realtimeMessage !== undefined) {
        return realtimeMessage || fallbackMessage;
    }

    return persistedMessage || fallbackMessage;
}
