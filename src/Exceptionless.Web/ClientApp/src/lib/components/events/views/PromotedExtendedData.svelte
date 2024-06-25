<script lang="ts">
    import { toast } from 'svelte-sonner';

    import { mutateDemoteTab } from '$api/projectsApi.svelte';
    import type { PersistentEvent } from '$lib/models/api';
    import ExtendedDataItem from '../ExtendedDataItem.svelte';
    import type { IFilter } from '$comp/filters/filters';

    interface Props {
        event: PersistentEvent;
        title: string;
        changed: (filter: IFilter) => void;
        demoted: (name: string) => void;
    }

    let { event, title, changed, demoted }: Props = $props();

    const demoteTab = mutateDemoteTab(event.project_id ?? '');
    demoteTab.subscribe((response) => {
        if (response.isError) {
            toast.error(`An error occurred demoting tab ${response.variables.name}`);
        } else if (response.isSuccess) {
            demoted(response.variables.name);
        }
    });

    function onDemote({ detail }: CustomEvent<string>): void {
        $demoteTab.mutate({ name: detail });
    }
</script>

<ExtendedDataItem {title} isPromoted={true} data={event.data?.[title]} on:demote={onDemote}></ExtendedDataItem>
