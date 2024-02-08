<script lang="ts">
    import { cn } from '$lib/utils';

    let className: string | undefined | null = undefined;
    export { className as class };
    export let items: unknown[] = [];
</script>

<ul class={cn('my-6 ml-6 list-disc [&>li]:mt-2', className)}>
    {#each items as item}
        <li><slot {item}>{item}</slot></li>
    {/each}
</ul>
