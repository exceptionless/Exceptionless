<script lang="ts">
    import type { IFilter } from '$comp/faceted-filter';
    import type { ProblemDetails } from '@exceptionless/fetchclient';

    import { resolve } from '$app/paths';
    import { Button } from '$comp/ui/button';
    import * as Sheet from '$comp/ui/sheet';
    import ExternalLink from '@lucide/svelte/icons/external-link';

    import IssueDetails from './issue-details.svelte';

    interface Props {
        filterChanged: (filter: IFilter) => void;
        onClose: () => void;
        onError?: (problem: ProblemDetails) => void;
        stackId: null | string | undefined;
    }

    let { filterChanged, onClose, onError, stackId = $bindable() }: Props = $props();

    const resolvedHref = $derived(stackId ? resolve('/(app)/issues/[stackId=objectid]', { stackId }) : '#');

    function handleOpenChange() {
        onClose();
    }

    function handleError(problem: ProblemDetails) {
        if (onError) {
            onError(problem);
        } else {
            onClose();
        }
    }
</script>

<Sheet.Root onOpenChange={handleOpenChange} open={!!stackId}>
    <Sheet.Content
        class="top-15.25! bottom-0! h-auto! w-full transform-gpu overflow-y-auto rounded-l-lg border-l shadow-2xl duration-150 ease-out will-change-transform sm:max-w-full! md:w-5/6!"
        overlayProps={{ class: 'top-15.25! bg-black/5 supports-backdrop-filter:backdrop-blur-none!' }}
    >
        <Sheet.Header>
            <Sheet.Title class="flex items-center gap-2">
                Issue Details
                <Button aria-label="Open issue details in new window" href={resolvedHref} size="icon-sm" title="Open in new window" variant="ghost">
                    <ExternalLink aria-hidden="true" />
                </Button>
            </Sheet.Title>
        </Sheet.Header>
        <div class="px-4">
            {#if stackId}
                <IssueDetails {filterChanged} {handleError} {stackId} />
            {/if}
        </div>
    </Sheet.Content>
</Sheet.Root>
