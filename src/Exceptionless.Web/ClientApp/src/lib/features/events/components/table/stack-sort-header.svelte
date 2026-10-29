<script lang="ts">
    import ArrowDown from '@lucide/svelte/icons/arrow-down';

    import { Button } from '#comp/ui/button/index.ts';

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
