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

export function createProductTourActions() {
    const controls = tryUseProductTourControls();
    const progressMutation = putCurrentUserProductTour();

    async function complete(checkpoint: ProductTourCheckpoint): Promise<boolean> {
        return finish(checkpoint, 'completed');
    }

    async function dismiss(checkpoint: ProductTourCheckpoint): Promise<boolean> {
        return finish(checkpoint, 'dismissed');
    }

    async function finish(checkpoint: ProductTourCheckpoint, action: 'completed' | 'dismissed'): Promise<boolean> {
        if (!productTourCheckpoint.clear(checkpoint)) {
            return false;
        }

        if (action === 'completed') {
            void progressMutation
                .mutateAsync({
                    tourName: checkpoint.tourName,
                    userId: checkpoint.userId
                })
                .catch(() => undefined);
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
        dismiss
    };
}
