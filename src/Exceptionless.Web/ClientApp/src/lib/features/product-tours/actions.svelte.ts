import { submitFeatureUsage } from '$features/auth/exceptionless-session';
import { putCurrentUserProductTour } from '$features/users/api.svelte';
import { ProductTourStatus } from '$features/users/models';
import { toast } from 'svelte-sonner';

import type { ProductTourCheckpoint, ProductTourKey, ProductTourLaunchSource } from './types';

import { tryUseProductTourControls } from './controls.svelte';
import { productTourCheckpoint } from './state.svelte';
import { buildProductTourTelemetryEvent, type ProductTourTelemetryEvent } from './telemetry';

const COMPLETION_MESSAGES: Record<Exclude<ProductTourCheckpoint['tourName'], 'app-overview'>, string> = {
    'event-investigate': 'You’ve explored an error and its occurrences',
    'exie-overview': 'You’re ready to ask Exie a question',
    'project-configure': 'Your project received its first event',
    'saved-view-create': 'Your saved view is ready'
};

const domainCompletionRequests = new WeakSet<ProductTourCheckpoint>();

export function createProductTourActions() {
    const controls = tryUseProductTourControls();
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
                    showCompletion(checkpoint);
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
            showCompletion(checkpoint);
        }
        return true;
    }

    function showCompletion(checkpoint: ProductTourCheckpoint): void {
        // The overview hands off to the Help menu; a toast would cover that menu.
        if (checkpoint.tourName !== 'app-overview') {
            toast.success(COMPLETION_MESSAGES[checkpoint.tourName], {
                action: controls
                    ? {
                          label: 'Browse guides',
                          onClick: controls.openCatalog
                      }
                    : undefined,
                description: 'For more guides, select your name in the sidebar → Help → Guided Tours.'
            });
        }
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
