<script lang="ts">
    import Filter from '@lucide/svelte/icons/filter';

    import { Button, type ButtonProps } from '#comp/ui/button/index.ts';

    import { EnvironmentFilter } from './models.svelte';

    type Props = Omit<ButtonProps, 'value'> & {
        changed: (filter: EnvironmentFilter) => void;
        /** How the environment is shown, which is the listed casing for a listed environment. */
        label?: null | string;
        value?: null | string;
    };
    let { changed, children, label, value, ...props }: Props = $props();
</script>

<Button
    variant="ghost"
    size={children ? 'xs' : 'icon-xs'}
    onclick={() => changed(new EnvironmentFilter([value ?? '']))}
    title={`Filter by environment: ${label ?? value ?? 'Unspecified'}`}
    {...props}
>
    {#if children}{@render children()}{:else}<Filter class="text-muted-foreground" />{/if}
</Button>
