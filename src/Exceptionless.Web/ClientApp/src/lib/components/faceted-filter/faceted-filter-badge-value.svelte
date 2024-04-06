<script lang="ts">
    import Badge from '$comp/ui/badge/badge.svelte';
</script>

<Badge variant="secondary" class="rounded-sm px-1 font-normal lg:hidden">
    <span class="max-w-24 truncate"><slot /></span>
</Badge>
<div class="hidden space-x-1 lg:flex">
    <Badge variant="secondary" class="rounded-sm px-1 font-normal">
        <span class="max-w-60 truncate"><slot /></span>
    </Badge>
</div>
