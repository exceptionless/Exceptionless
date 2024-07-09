<script lang="ts">
    import { toast } from 'svelte-sonner';

    import { mutateDemoteTab } from '$api/projectsApi.svelte';
    import type { PersistentEvent } from '$lib/models/api';
    import ExtendedDataItem from '../ExtendedDataItem.svelte';

    interface Props {
        event: PersistentEvent;
        title: string;
        demoted: (name: string) => void;
    }

    let { event, title, demoted }: Props = $props();

    const demoteTab = mutateDemoteTab({
        get id() {
            return event.project_id!;
        }
    });

    function onDemote(title: string): void {
        demoteTab.mutate({ name: title });
    }

    $effect(() => {
        if (demoteTab.isError) {
            toast.error(`An error occurred demoting tab ${demoteTab.variables.name}`);
        } else if (demoteTab.isSuccess) {
            demoted(demoteTab.variables.name);
        }
    });
</script>

<ExtendedDataItem {title} isPromoted={true} data={event.data?.[title]} demote={onDemote}></ExtendedDataItem>
