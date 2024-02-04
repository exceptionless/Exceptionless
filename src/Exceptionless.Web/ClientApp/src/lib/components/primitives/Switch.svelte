<script lang="ts">
    import { Switch } from '$comp/ui/switch';
    import Label from '$comp/ui/label/label.svelte';

    export let checked: boolean | undefined;
    export let disabled: boolean = false;
    export let id: string;
</script>

<div class="flex items-center space-x-2">
    <Switch {id} bind:checked on:click {disabled} />
    <Label for={id}>
        <slot />
    </Label>
</div>
