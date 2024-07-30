<script lang="ts">
    import type { Snippet } from 'svelte';
    import { Switch } from '$comp/ui/switch';
    import Label from '$comp/ui/label/label.svelte';

    interface Props {
        children: Snippet;
        checked?: boolean;
        disabled: boolean;
        id: string;
    }

    let { children, checked = $bindable(), disabled = false, id }: Props = $props();
</script>

<div class="flex items-center space-x-2">
    <Switch {id} bind:checked on:click {disabled} />
    <Label for={id}>
        {@render children()}
    </Label>
</div>
