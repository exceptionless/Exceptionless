import type { ProductTourProgress } from '$features/users/models';

export function shouldOfferProductTourWelcome(progress: ProductTourProgress | undefined, welcomeVersion: number): boolean {
    return !progress || progress.version < welcomeVersion;
}
