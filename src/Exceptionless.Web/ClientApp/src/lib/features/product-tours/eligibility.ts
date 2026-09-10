import type { RouteId } from '$app/types';
import type { ProductTourState } from '$features/users/models';

const SETUP_ROUTE_IDS = new Set<RouteId>(['/(app)/organization/add', '/(app)/project/[projectId]/configure', '/(app)/project/add']);

export function getProductTourRecordedAt(state: ProductTourState = {}, key: string, kind: 'guide' | 'invitation' = 'guide'): string | undefined {
    const values = state as Record<string, unknown>;
    const value = values[key] ?? values[key.replaceAll('_', '-')];
    if (typeof value === 'string') {
        return value;
    }

    // Earlier clients stored progress objects under hyphenated keys.
    if (value && typeof value === 'object' && 'status' in value && 'updated_utc' in value) {
        const acknowledged = value.status === 'completed' || (kind === 'invitation' && value.status === 'dismissed');
        if (acknowledged && typeof value.updated_utc === 'string') {
            return value.updated_utc;
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
