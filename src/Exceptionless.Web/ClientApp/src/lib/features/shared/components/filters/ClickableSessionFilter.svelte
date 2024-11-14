<script lang="ts">
    import { A, type AProps } from '$comp/typography';
    import { cn } from '$lib/utils';
    import IconFilter from '~icons/mdi/filter';

    import { SessionFilter } from './filters.svelte';

    type Props = {
        changed: (filter: SessionFilter) => void;
        value: string;
    } & AProps;
    let { changed, class: className, value, ...props }: Props = $props();

    const title = `Search ref.session:${value}`;
</script>

<A class={cn('cursor-pointer', className)} onclick={() => changed(new SessionFilter(value))} {title} {...props}>
    {#snippet children()}
        <IconFilter class="text-muted-foreground text-opacity-50 hover:text-primary" />
    {/snippet}
</A>
