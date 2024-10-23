<script lang="ts">
    import { A, type AProps } from '$comp/typography';
    import IconFilter from '~icons/mdi/filter';

    import { ProjectFilter } from './filters.svelte';

    type Props = {
        changed: (filter: ProjectFilter) => void;
        organization: string;
        value: string[];
    } & AProps;
    let { changed, organization, value, ...props }: Props = $props();

    const title = `Search project:${value}`;
</script>

<A class="cursor-pointer" onclick={() => changed(new ProjectFilter(organization, value))} {title} {...props}>
    {#snippet children()}
        <IconFilter class="text-muted-foreground text-opacity-50 hover:text-primary" />
    {/snippet}
</A>
