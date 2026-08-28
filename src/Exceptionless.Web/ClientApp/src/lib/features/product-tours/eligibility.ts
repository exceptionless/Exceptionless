import type { ProductTourProgress } from '$features/users/models';

const SETUP_ROUTE_IDS = new Set(['/(app)/organization/add', '/(app)/project/[projectId]/configure', '/(app)/project/add']);

export function isProductTourSetupRoute(routeId: null | string): boolean {
    return !!routeId && SETUP_ROUTE_IDS.has(routeId);
}

export function shouldOfferProductTourAnnouncement(progress: ProductTourProgress | undefined, announcementVersion: number): boolean {
    return !progress || progress.version < announcementVersion;
}

export function shouldOfferProductTourWelcome(progress: ProductTourProgress | undefined, welcomeVersion: number): boolean {
    return !progress || progress.version < welcomeVersion;
}
