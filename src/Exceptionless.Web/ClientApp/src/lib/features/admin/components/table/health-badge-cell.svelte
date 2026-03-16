<script lang="ts">
    import { Badge } from '$comp/ui/badge';
    import { healthBadgeClass, healthLabel, healthVariant } from '$features/admin/elasticsearch-utils';

    interface Props {
        value: null | string | undefined;
    }

    let { value }: Props = $props();
</script>

<Badge class="{healthBadgeClass(value)} text-xs" variant={healthVariant(value)}>
    {healthLabel(value)}
</Badge>
