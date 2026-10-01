<script lang="ts">
    let { flag = false }: { flag?: boolean } = $props();
    const value = $derived(flag ? 'left' : 'right');
</script>

<p>{value}</p>
