<script lang="ts">
    import type { Snippet } from 'svelte';

    import Label from '$comp/ui/label/label.svelte';
    import { Switch } from '$comp/ui/switch';

    interface Props {
        checked?: boolean;
        children?: Snippet;
        disabled?: boolean;
        id: string;
    }

    let { checked = $bindable(), children, disabled = false, id }: Props = $props();
</script>

<div class="flex items-center space-x-2">
    <Switch bind:checked {disabled} {id} on:click />
    <Label for={id}>
        {#if children}
            {@render children()}
        {/if}
    </Label>
</div>
