import type { RouteId } from '$app/types';
import type { ProductTourState } from '$features/users/models';

const SETUP_ROUTE_IDS = new Set<RouteId>(['/(app)/organization/add', '/(app)/project/[projectId]/configure', '/(app)/project/add']);
const LEGACY_STATE_KEYS: Record<string, readonly string[]> = {
    app_overview: ['ui-overview', 'new-ui-overview'],
    app_welcome: ['welcome'],
    event_investigate: ['investigate-error'],
    exie_overview: ['meet-exie'],
    project_configure: ['configure-project'],
    saved_view_create: ['create-saved-view']
};

export function getProductTourRecordedAt(state: ProductTourState = {}, key: string, kind: 'guide' | 'invitation' = 'guide'): string | undefined {
    const values = state as Record<string, unknown>;
    const keys = [key, key.replaceAll('_', '-'), ...(LEGACY_STATE_KEYS[key] ?? [])];
    for (const stateKey of keys) {
        const value = values[stateKey];
        if (typeof value === 'string') {
            return value;
        }

        // Earlier clients stored progress objects under different tour names.
        if (value && typeof value === 'object' && 'status' in value && 'updated_utc' in value) {
            const acknowledged = value.status === 'completed' || (kind === 'invitation' && value.status === 'dismissed');
            if (acknowledged && typeof value.updated_utc === 'string') {
                return value.updated_utc;
            }
        }
    }

    return undefined;
}

export function isProductTourSetupRoute(routeId: null | RouteId): boolean {
    return !!routeId && SETUP_ROUTE_IDS.has(routeId);
}

export function shouldOfferProductTourInvitation(recordedAt?: null | string): boolean {
    return !recordedAt;
}
