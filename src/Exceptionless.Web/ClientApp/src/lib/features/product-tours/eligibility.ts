const SETUP_ROUTE_IDS = new Set(['/(app)/organization/add', '/(app)/project/[projectId]/configure', '/(app)/project/add']);

export function isProductTourSetupRoute(routeId: null | string): boolean {
    return !!routeId && SETUP_ROUTE_IDS.has(routeId);
}

export function shouldOfferProductTourInvitation(recordedAt?: null | string): boolean {
    return !recordedAt;
}
