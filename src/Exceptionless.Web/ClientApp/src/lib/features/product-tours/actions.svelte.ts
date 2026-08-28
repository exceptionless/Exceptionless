import { submitFeatureUsage } from '$features/auth/exceptionless-session';
import { putCurrentUserProductTour } from '$features/users/api.svelte';
import { ProductTourStatus } from '$features/users/models';
import { toast } from 'svelte-sonner';

import type { ProductTourCheckpoint, ProductTourKey, ProductTourLaunchSource } from './types';

import { getProductTour } from './catalog';
import { productTourCheckpoint } from './state.svelte';
import { buildProductTourTelemetryEvent, type ProductTourTelemetryEvent } from './telemetry';

export function createProductTourActions() {
    const progressMutation = putCurrentUserProductTour();

    async function complete(checkpoint: ProductTourCheckpoint): Promise<boolean> {
        return finish(checkpoint, ProductTourStatus.Completed);
    }

    async function dismiss(checkpoint: ProductTourCheckpoint): Promise<boolean> {
        return finish(checkpoint, ProductTourStatus.Dismissed);
    }

    async function finish(checkpoint: ProductTourCheckpoint, status: ProductTourStatus): Promise<boolean> {
        const definition = getProductTour(checkpoint.tourName);
        try {
            await progressMutation.mutateAsync({
                progress: {
                    status,
                    version: definition.version
                },
                tourName: checkpoint.tourName
            });
        } catch {
            toast.error('We could not save your guided-tour progress. Please try again.');
            return false;
        }

        if (!productTourCheckpoint.clear(checkpoint)) {
            return false;
        }
        await track(status === ProductTourStatus.Completed ? 'completed' : 'dismissed', checkpoint.tourName, definition.version, checkpoint.source);
        return true;
    }

    return {
        complete,
        dismiss,
        progressMutation
    };
}

export async function track(event: ProductTourTelemetryEvent, name: ProductTourKey, version: number, source: ProductTourLaunchSource): Promise<void> {
    try {
        await submitFeatureUsage(buildProductTourTelemetryEvent(event, name, version, source));
    } catch (error) {
        console.warn('Unable to submit product tour telemetry.', error);
    }
}
