<script lang="ts">
    import type { IFilter } from '$comp/faceted-filter';
    import type { ProblemDetails } from '@foundatiofx/fetchclient';

    import { goto } from '$app/navigation';
    import { resolve } from '$app/paths';
    import ErrorMessage from '$comp/error-message.svelte';
    import { organization } from '$features/organizations/context.svelte';
    import { getProblemMessage } from '$shared/validation';

    import type { Stack } from '../models';

    import { postChangeStatus, postMarkFixed } from '../api.svelte';
    import { StackStatus } from '../models';
    import MarkStackDiscardedDialog from './dialogs/mark-stack-discarded-dialog.svelte';
    import MarkStackFixedInVersionDialog from './dialogs/mark-stack-fixed-in-version-dialog.svelte';
    import MarkStackIgnoredDialog from './dialogs/mark-stack-ignored-dialog.svelte';
    import StackCard from './stack-card.svelte';

    interface Props {
        action: 'discarded' | 'ignored' | 'mark-fixed';
        filterChanged: (filter: IFilter) => void;
        stackId: string;
    }

    let { action, filterChanged, stackId }: Props = $props();
    let stack = $state<Stack>();
    let error = $state<string>();
    let open = $state(true);

    const markFixed = postMarkFixed({
        route: {
            get ids() {
                return [stackId];
            }
        }
    });
    const changeStatus = postChangeStatus({
        route: {
            get ids() {
                return [stackId];
            }
        }
    });

    async function discard() {
        await changeStatus.mutateAsync(StackStatus.Discarded);
    }

    function handleError(problem: ProblemDetails) {
        error = getProblemMessage(problem, 'Unable to load this stack.');
    }

    function handleLoaded(value: Stack) {
        stack = value;
        organization.current = value.organization_id;
    }

    async function handleOpenChange(value: boolean) {
        open = value;
        if (!value) {
            await goto(
                resolve('/(app)/stack/[stackId=objectid]', {
                    stackId
                }),
                {
                    replaceState: true
                }
            );
        }
    }

    async function ignore() {
        await changeStatus.mutateAsync(StackStatus.Ignored);
    }

    async function save(version?: string) {
        await markFixed.mutateAsync(version);
    }
</script>

<ErrorMessage message={error} />
<StackCard id={stackId} {filterChanged} onLoaded={handleLoaded} onError={handleError} />

{#if stack}
    {#if action === 'mark-fixed'}
        <MarkStackFixedInVersionDialog bind:open={() => open, handleOpenChange} {save} stackTitle={stack.title} />
    {:else if action === 'ignored'}
        <MarkStackIgnoredDialog bind:open={() => open, handleOpenChange} {ignore} stackTitle={stack.title} />
    {:else}
        <MarkStackDiscardedDialog bind:open={() => open, handleOpenChange} {discard} stackTitle={stack.title} />
    {/if}
{/if}
