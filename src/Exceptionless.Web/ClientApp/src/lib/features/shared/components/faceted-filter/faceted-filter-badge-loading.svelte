<script lang="ts">
    import Badge from '$comp/ui/badge/badge.svelte';
    import { Spinner } from '$comp/ui/spinner';
</script>

<Badge class="rounded-sm px-1 font-normal" variant="secondary">
    <Spinner /> Loading
</Badge>
