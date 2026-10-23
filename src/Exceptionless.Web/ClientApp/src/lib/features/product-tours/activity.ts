import { submitFeatureUsage } from '#features/auth/exceptionless-session.ts';

import type { ProductTourKey } from './models';

export async function submitProductTourActivity(action: 'completed' | 'dismissed', name: ProductTourKey): Promise<void> {
    await submitFeatureUsage(`product-tour.${action}.${name}`);
}
