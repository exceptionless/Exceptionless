<script lang="ts">
    import type { IFilter } from '$comp/faceted-filter';

    import { organization } from '$features/organizations/context.svelte';
    import StackActionPage from '$features/stacks/components/stack-action-page.svelte';

    import type { PageProps } from './$types';

    import { getEventsNavigationOptionsForFilter, redirectToEventsWithFilter } from '../../../redirect-to-events.svelte.js';

    let { params }: PageProps = $props();

    async function filterChanged(filter: IFilter) {
        await redirectToEventsWithFilter(organization.current, filter, getEventsNavigationOptionsForFilter(filter));
    }
</script>

<svelte:head>
    <title>Manage Stack - Exceptionless</title>
</svelte:head>

{#key `${params.stackId}/${params.action}`}
    <StackActionPage stackId={params.stackId} action={params.action} {filterChanged} />
{/key}
