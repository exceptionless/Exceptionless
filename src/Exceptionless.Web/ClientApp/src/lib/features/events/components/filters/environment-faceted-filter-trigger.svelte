<script lang="ts">
    import { Button, type ButtonProps } from '$comp/ui/button';
    import Filter from '@lucide/svelte/icons/filter';

    import { EnvironmentFilter } from './models.svelte';

    type Props = Omit<ButtonProps, 'value'> & {
        changed: (filter: EnvironmentFilter) => void;
        value?: null | string;
    };
    let { changed, children, value, ...props }: Props = $props();
</script>

<Button
    variant="ghost"
    size={children ? 'xs' : 'icon-xs'}
    onclick={() => changed(new EnvironmentFilter([value ?? '']))}
    title={`Filter by environment: ${value ?? 'Unspecified'}`}
    {...props}
>
    {#if children}{@render children()}{:else}<Filter class="text-muted-foreground" />{/if}
</Button>
