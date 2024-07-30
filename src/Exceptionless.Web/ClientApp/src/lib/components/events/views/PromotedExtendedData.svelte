<script lang="ts">
    import type { PersistentEvent } from '$lib/models/api';

    import { mutateDemoteTab } from '$api/projectsApi.svelte';
    import { toast } from 'svelte-sonner';

    import ExtendedDataItem from '../ExtendedDataItem.svelte';

    interface Props {
        demoted: (name: string) => void;
        event: PersistentEvent;
        title: string;
    }

    let { demoted, event, title }: Props = $props();

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
