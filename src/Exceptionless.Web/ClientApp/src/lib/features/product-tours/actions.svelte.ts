import { putCurrentUserProductTour } from '$features/users/api.svelte';
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
const STATE_KEYS: Record<
    ProductTourCheckpoint['tourName'],
    'app_overview' | 'event_investigate' | 'exie_overview' | 'project_configure' | 'saved_view_create'
> = {
    'app-overview': 'app_overview',
    'event-investigate': 'event_investigate',
    'exie-overview': 'exie_overview',
    'project-configure': 'project_configure',
    'saved-view-create': 'saved_view_create'
};

export function createProductTourActions() {
    const controls = tryUseProductTourControls();
    const progressMutation = putCurrentUserProductTour();

    async function complete(checkpoint: ProductTourCheckpoint): Promise<boolean> {
        return finish(checkpoint, 'completed');
    }

    async function dismiss(checkpoint: ProductTourCheckpoint): Promise<boolean> {
        return finish(checkpoint, 'dismissed');
    }

    async function completeAfterDomainSuccess(checkpoint: ProductTourCheckpoint): Promise<void> {
        finish(checkpoint, 'completed');
    }

    async function finish(checkpoint: ProductTourCheckpoint, action: 'completed' | 'dismissed'): Promise<boolean> {
        if (productTourCheckpoint.current !== checkpoint || progressRequests.has(checkpoint)) {
            return false;
        }

        progressRequests.add(checkpoint);
        if (!productTourCheckpoint.clear(checkpoint)) {
            progressRequests.delete(checkpoint);
            return false;
        }

        if (action === 'completed') {
            void Promise.resolve(
                progressMutation.mutateAsync({
                    recordName: checkpoint.tourName,
                    stateKey: STATE_KEYS[checkpoint.tourName],
                    userId: checkpoint.userId
                })
            )
                .catch(() => undefined)
                .finally(() => progressRequests.delete(checkpoint));
        } else {
            progressRequests.delete(checkpoint);
        }
        void submitProductTourActivity(action, checkpoint.tourName);
        if (action === 'completed') {
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
