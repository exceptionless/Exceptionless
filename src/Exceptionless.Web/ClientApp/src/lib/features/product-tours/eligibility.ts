import type { RouteId } from '$app/types';

const SETUP_ROUTE_IDS = new Set<RouteId>(['/(app)/organization/add', '/(app)/project/[projectId]/configure', '/(app)/project/add']);

export function isProductTourSetupRoute(routeId: null | RouteId): boolean {
    return !!routeId && SETUP_ROUTE_IDS.has(routeId);
}

export function shouldOfferProductTourInvitation(recordedAt?: null | string): boolean {
    return !recordedAt;
}
