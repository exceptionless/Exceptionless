<script lang="ts">
    import { A, type AProps } from '$comp/typography';
    import { cn } from '$lib/utils';
    import Filter from '@lucide/svelte/icons/filter';

    import { ProjectFilter } from './models.svelte';

    type Props = AProps & {
        changed: (filter: ProjectFilter) => void;
        value: string[];
    };
    let { changed, class: className, value, ...props }: Props = $props();
    const title = `Search project:${value}`;
</script>

<A class={cn('cursor-pointer', className)} onclick={() => changed(new ProjectFilter(value))} {title} {...props}>
    <Filter class="text-muted-foreground text-opacity-50 hover:text-primary size-5" />
</A>
