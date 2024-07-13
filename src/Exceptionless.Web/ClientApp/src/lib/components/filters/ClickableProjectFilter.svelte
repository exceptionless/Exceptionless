<script lang="ts">
    import IconFilter from '~icons/mdi/filter';
    import { A, type AProps } from '$comp/typography';
    import { ProjectFilter } from './filters.svelte';

    type Props = AProps & {
        changed: (filter: ProjectFilter) => void;
        organization: string;
        value: string[];
    };
    let { changed, organization, value, ...props }: Props = $props();

    const title = `Search project:${value}`;
</script>

<A onclick={() => changed(new ProjectFilter(organization, value))} {title} {...props}>
    {#snippet children()}
        <IconFilter class="text-muted-foreground text-opacity-50 hover:text-primary" />
    {/snippet}
</A>
