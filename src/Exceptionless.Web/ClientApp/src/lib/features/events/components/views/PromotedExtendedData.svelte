<script lang="ts">
    import { mutateDemoteTab } from '$features/projects/api.svelte';
    import { toast } from 'svelte-sonner';

    import type { PersistentEvent } from '../../models/index';

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
