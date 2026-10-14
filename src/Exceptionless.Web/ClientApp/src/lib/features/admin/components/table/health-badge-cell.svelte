<script lang="ts">
    import { Badge } from '#comp/ui/badge/index.ts';
    import { healthBadgeClass, healthLabel, healthVariant } from '#features/admin/elasticsearch-utils.ts';

    interface Props {
        value: null | string | undefined;
    }

    let { value }: Props = $props();
</script>

<Badge class="{healthBadgeClass(value)} text-xs" variant={healthVariant(value)}>
    {healthLabel(value)}
</Badge>
