<script lang="ts">
    import type { ViewProject } from '$features/projects/models';

    import { getExtendedDataItems } from '$features/events/persistent-event';
    import { postPromotedTab } from '$features/projects/api.svelte';
    import { toast } from 'svelte-sonner';

    import type { PersistentEvent } from '../../models/index';

    import ExtendedDataItem from '../ExtendedDataItem.svelte';

    interface Props {
        event: PersistentEvent;
        project?: ViewProject;
        promoted: (name: string) => void;
    }

    let { event, project, promoted }: Props = $props();
    let items = $derived(getExtendedDataItems(event, project));

    const promoteTab = postPromotedTab({
        route: {
            get id() {
                return event.project_id!;
            }
        }
    });

    async function onPromote(title: string): Promise<void> {
        const wasPromoted = await promoteTab.mutateAsync({ name: title });
        if (wasPromoted) {
            promoted(title);
        } else {
            toast.error(`An error occurred promoting tab ${title}`);
        }
    }
</script>

<div class="space-y-4">
    {#each items as { data, promoted, title }}
        {#if promoted === false}
            <div data-id={title}>
                <ExtendedDataItem {data} promote={onPromote} {title}></ExtendedDataItem>
            </div>
        {/if}
    {/each}
</div>
