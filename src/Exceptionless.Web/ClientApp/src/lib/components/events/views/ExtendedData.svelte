<script lang="ts">
    import { toast } from 'svelte-sonner';

    import type { PersistentEvent, ViewProject } from '$lib/models/api';
    import ExtendedDataItem from '../ExtendedDataItem.svelte';
    import { getExtendedDataItems } from '$lib/helpers/persistent-event';
    import { mutatePromoteTab } from '$api/projectsApi.svelte';

    interface Props {
        event: PersistentEvent;
        project?: ViewProject;
        promoted: (name: string) => void;
    }

    let { event, project, promoted }: Props = $props();
    let items = $derived(getExtendedDataItems(event, project));

    const promoteTab = mutatePromoteTab(event.project_id ?? '');
    promoteTab.subscribe((response) => {
        if (response.isError) {
            toast.error(`An error occurred promoting tab ${response.variables.name}`);
        } else if (response.isSuccess) {
            promoted(response.variables.name);
        }
    });

    function onPromote(name: string): void {
        $promoteTab.mutate({ name });
    }
</script>

<div class="space-y-4">
    {#each items as { title, promoted, data }}
        {#if promoted === false}
            <div data-id={title}>
                <ExtendedDataItem {title} {data} promote={onPromote}></ExtendedDataItem>
            </div>
        {/if}
    {/each}
</div>
