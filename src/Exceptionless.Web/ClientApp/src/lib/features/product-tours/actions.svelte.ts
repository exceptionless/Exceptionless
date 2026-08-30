import { submitFeatureUsage } from '$features/auth/exceptionless-session';
import { putCurrentUserProductTour } from '$features/users/api.svelte';
import { ProductTourStatus } from '$features/users/models';
import { toast } from 'svelte-sonner';

import type { ProductTourCheckpoint, ProductTourKey, ProductTourLaunchSource } from './types';

import { productTourCheckpoint } from './state.svelte';
import { buildProductTourTelemetryEvent, type ProductTourTelemetryEvent } from './telemetry';

const COMPLETION_NEXT_STEPS: Record<ProductTourCheckpoint['tourName'], string> = {
    'app-overview': 'Next: open Guided Tours from Help whenever you want a focused workflow.',
    'event-investigate': 'Next: save a useful Events view for the investigation you repeat most.',
    'exie-overview': 'Next: open Exie from a real event when you want help investigating it.',
    'project-configure': 'Next: open the received event and investigate its stack.',
    'saved-view-create': 'Next: reuse the saved view from the application navigation.'
};

const domainCompletionRequests = new WeakSet<ProductTourCheckpoint>();

export function createProductTourActions() {
    const progressMutation = putCurrentUserProductTour();

    async function complete(checkpoint: ProductTourCheckpoint): Promise<boolean> {
        return finish(checkpoint, ProductTourStatus.Completed);
    }

    async function dismiss(checkpoint: ProductTourCheckpoint): Promise<boolean> {
        return finish(checkpoint, ProductTourStatus.Dismissed);
    }

    function completeAfterDomainSuccess(checkpoint: ProductTourCheckpoint): void {
        if (productTourCheckpoint.current !== checkpoint || domainCompletionRequests.has(checkpoint)) {
            return;
        }

        domainCompletionRequests.add(checkpoint);
        void progressMutation
            .mutateAsync({
                progress: {
                    status: ProductTourStatus.Completed,
                    version: checkpoint.version
                },
                tourName: checkpoint.tourName
            })
            .then(() => {
                if (productTourCheckpoint.clear(checkpoint)) {
                    void track('completed', checkpoint.tourName, checkpoint.version, checkpoint.source);
                }
            })
            .catch(() => {
                toast.error('Setup succeeded, but guided-tour progress could not be saved.');
            })
            .finally(() => {
                domainCompletionRequests.delete(checkpoint);
            });
    }

    async function finish(checkpoint: ProductTourCheckpoint, status: ProductTourStatus): Promise<boolean> {
        try {
            await progressMutation.mutateAsync({
                progress: {
                    status,
                    version: checkpoint.version
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
        void track(status === ProductTourStatus.Completed ? 'completed' : 'dismissed', checkpoint.tourName, checkpoint.version, checkpoint.source);
        if (status === ProductTourStatus.Completed) {
            toast.success('Guide complete', {
                description: COMPLETION_NEXT_STEPS[checkpoint.tourName]
            });
        }
        return true;
    }

    return {
        complete,
        completeAfterDomainSuccess,
        dismiss
    };
}

export async function track(event: ProductTourTelemetryEvent, name: ProductTourKey, version: number, source: ProductTourLaunchSource): Promise<void> {
    try {
        await submitFeatureUsage(buildProductTourTelemetryEvent(event, name, version, source));
    } catch (error) {
        console.warn('Unable to submit product tour telemetry.', error);
    }
}
