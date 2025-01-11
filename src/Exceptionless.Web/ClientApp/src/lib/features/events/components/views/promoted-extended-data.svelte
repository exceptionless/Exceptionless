<script lang="ts">
    import { deletePromotedTab } from '$features/projects/api.svelte';
    import { toast } from 'svelte-sonner';

    import type { PersistentEvent } from '../../models/index';

    import ExtendedDataItem from '../extended-data-item.svelte';

    interface Props {
        demoted: (name: string) => void;
        event: PersistentEvent;
        title: string;
    }

    let { demoted, event, title }: Props = $props();

    const demoteTab = deletePromotedTab({
        route: {
            get id() {
                return event.project_id!;
            }
        }
    });

    async function onDemote(title: string): Promise<void> {
        const wasDemoted = await demoteTab.mutateAsync({ name: title });
        if (wasDemoted) {
            demoted(title);
        } else {
            toast.error(`An error occurred demoting tab ${title}`);
        }
    }
</script>

<ExtendedDataItem data={event.data?.[title]} demote={onDemote} isPromoted={true} {title}></ExtendedDataItem>
