<script lang="ts">
    import { Badge } from '$comp/ui/badge';
    import { snapshotBadgeClass, snapshotVariant } from '$features/admin/elasticsearch-utils';

    interface Props {
        value: string | undefined;
    }

    let { value }: Props = $props();
</script>

<Badge class="{snapshotBadgeClass(value)} text-xs capitalize" variant={snapshotVariant(value)}>
    {value?.toLowerCase().replace('_', ' ')}
</Badge>
