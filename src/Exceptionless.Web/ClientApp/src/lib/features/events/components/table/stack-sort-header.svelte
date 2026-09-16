<script lang="ts">
    import { Button } from '$comp/ui/button';
    import ArrowDown from '@lucide/svelte/icons/arrow-down';

    interface Props {
        active: boolean;
        label: string;
        onclick: () => void;
    }

    let { active, label, onclick }: Props = $props();
</script>

<Button aria-label={`Sort by ${label} descending`} aria-pressed={active} class="h-8" {onclick} variant="ghost">
    {label}
    {#if active}
        <ArrowDown aria-hidden="true" />
    {/if}
</Button>
