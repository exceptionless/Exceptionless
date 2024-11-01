<script lang="ts">
    import { A, type AProps } from '$comp/typography';
    import { cn } from '$lib/utils';
    import IconFilter from '~icons/mdi/filter';

    import { OrganizationFilter } from './filters.svelte';

    type Props = {
        changed: (filter: OrganizationFilter) => void;
        value: string;
    } & AProps;
    let { changed, class: className, value, ...props }: Props = $props();

    const title = `Search organization:${value}`;
</script>

<A class={cn('cursor-pointer', className)} onclick={() => changed(new OrganizationFilter(value))} {title} {...props}>
    {#snippet children()}
        <IconFilter class="text-muted-foreground text-opacity-50 hover:text-primary" />
    {/snippet}
</A>
