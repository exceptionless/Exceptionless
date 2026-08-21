<script lang="ts">
    import type { CustomFieldDefinition } from '$features/organizations/custom-fields';

    import { deletePromotedTab } from '$features/projects/api.svelte';
    import { toast } from 'svelte-sonner';

    import type { PersistentEvent } from '../../models/index';

    import ExtendedDataItem from '../extended-data-item.svelte';

    interface Props {
        customFields?: CustomFieldDefinition[];
        demoted: (name: string) => void;
        event: PersistentEvent;
        title: string;
    }

    let { customFields, demoted, event, title }: Props = $props();

    const demoteTab = deletePromotedTab({
        route: {
            get id() {
                return event.project_id!;
            }
        }
    });

    async function onDemote(title: string): Promise<void> {
        const wasDemoted = await demoteTab.mutateAsync({
            name: title
        });
        if (wasDemoted) {
            demoted(title);
        } else {
            toast.error(`An error occurred demoting tab ${title}`);
        }
    }
</script>

<ExtendedDataItem {customFields} data={event.data?.[title]} demote={onDemote} isPromoted={true} organizationId={event.organization_id} showTitle={false} {title}
></ExtendedDataItem>
