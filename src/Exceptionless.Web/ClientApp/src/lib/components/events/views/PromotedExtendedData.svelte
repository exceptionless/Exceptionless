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

    async function onDemote(title: string): Promise<void> {
        const response = await demoteTab.mutateAsync({ name: title });
        if (response.ok) {
            demoted(title);
        } else {
            toast.error(`An error occurred demoting tab ${title}`);
        }
    }
</script>

<ExtendedDataItem data={event.data?.[title]} demote={onDemote} isPromoted={true} {title}></ExtendedDataItem>
