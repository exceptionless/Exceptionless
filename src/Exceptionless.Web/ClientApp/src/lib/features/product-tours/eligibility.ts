import type { ProductTourProgress } from '$features/users/models';

export function shouldOfferProductTourAnnouncement(progress: ProductTourProgress | undefined, announcementVersion: number): boolean {
    return !progress || progress.version < announcementVersion;
}

export function shouldOfferProductTourWelcome(progress: ProductTourProgress | undefined, welcomeVersion: number): boolean {
    return !progress || progress.version < welcomeVersion;
}
