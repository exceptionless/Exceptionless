import { putCurrentUserProductTour } from '$features/users/api.svelte';
import { ProductTourStatus } from '$features/users/models';
import { toast } from 'svelte-sonner';

import type { ProductTourCheckpoint } from './models';

import { submitProductTourActivity } from './activity';
import { tryUseProductTourControls } from './controls.svelte';
import { productTourCheckpoint } from './state.svelte';

const COMPLETION_MESSAGES: Record<Exclude<ProductTourCheckpoint['tourName'], 'app-overview'>, string> = {
    'event-investigate': 'You’ve explored an error and its occurrences',
    'exie-overview': 'You’re ready to ask Exie a question',
    'project-configure': 'Your project received its first event',
    'saved-view-create': 'Your saved view is ready'
};

const progressRequests = new WeakSet<ProductTourCheckpoint>();

export function createProductTourActions() {
    const controls = tryUseProductTourControls();
    const progressMutation = putCurrentUserProductTour();

    async function complete(checkpoint: ProductTourCheckpoint): Promise<boolean> {
        return await finish(checkpoint, ProductTourStatus.Completed);
    }

    async function dismiss(checkpoint: ProductTourCheckpoint): Promise<boolean> {
        return await finish(checkpoint, ProductTourStatus.Dismissed);
    }

    async function completeAfterDomainSuccess(checkpoint: ProductTourCheckpoint): Promise<void> {
        await finish(checkpoint, ProductTourStatus.Completed, 'Setup succeeded, but guided-tour progress could not be saved.');
    }

    async function finish(
        checkpoint: ProductTourCheckpoint,
        status: ProductTourStatus,
        errorMessage = 'We could not save your guided-tour progress. Please try again.'
    ): Promise<boolean> {
        if (productTourCheckpoint.current !== checkpoint || progressRequests.has(checkpoint)) {
            return false;
        }

        progressRequests.add(checkpoint);
        try {
            await progressMutation.mutateAsync({
                progress: {
                    status,
                    version: checkpoint.version
                },
                tourName: checkpoint.tourName
            });
        } catch {
            toast.error(errorMessage);
            return false;
        } finally {
            progressRequests.delete(checkpoint);
        }

        if (!productTourCheckpoint.clear(checkpoint)) {
            return false;
        }
        await submitProductTourActivity(
            status === ProductTourStatus.Completed ? 'completed' : 'dismissed',
            checkpoint.tourName,
            checkpoint.version,
            checkpoint.source
        );
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
