<script lang="ts">
    import type { IFilter } from '$comp/faceted-filter';
    import type { ProblemDetails } from '@exceptionless/fetchclient';

    import { resolve } from '$app/paths';
    import DetailSheet from '$comp/detail-sheet.svelte';

    import StackDetails from './stack-details.svelte';

    interface Props {
        filterChanged: (filter: IFilter) => void;
        onClose: () => void;
        onError?: (problem: ProblemDetails) => void;
        stackId: null | string | undefined;
    }

    let { filterChanged, onClose, onError, stackId = $bindable() }: Props = $props();

    const resolvedHref = $derived(stackId ? resolve('/(app)/stacks/[stackId=objectid]', { stackId }) : '#');

    function handleError(problem: ProblemDetails) {
        if (onError) {
            onError(problem);
        } else {
            onClose();
        }
    }
</script>

<DetailSheet detailsHref={resolvedHref} {onClose} open={!!stackId} title="Stack">
    {#if stackId}
        <StackDetails {filterChanged} {handleError} {stackId} />
    {/if}
</DetailSheet>
