<script lang="ts">
    import { Badge } from '$comp/ui/badge';

    interface Props {
        scopes: string[];
    }

    let { scopes }: Props = $props();
</script>

<div class="flex flex-wrap gap-1">
    {#each scopes as scope (scope)}
        <Badge variant="secondary">{scope}</Badge>
    {/each}
</div>
