<script lang="ts">
    import { A, type AProps } from '$comp/typography';
    import { cn } from '$lib/utils';
    import IconFilter from '~icons/mdi/filter';

    import { ProjectFilter } from './filters.svelte';

    type Props = AProps & {
        changed: (filter: ProjectFilter) => void;
        organization: string;
        value: string[];
    };
    let { changed, class: className, organization, value, ...props }: Props = $props();

    const title = `Search project:${value}`;
</script>

<A class={cn('cursor-pointer', className)} onclick={() => changed(new ProjectFilter(organization, value))} {title} {...props}>
    {#snippet children()}
        <IconFilter class="text-muted-foreground text-opacity-50 hover:text-primary" />
    {/snippet}
</A>
