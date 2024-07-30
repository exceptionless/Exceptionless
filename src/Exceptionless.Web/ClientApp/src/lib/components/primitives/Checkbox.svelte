<script lang="ts">
    import type { Snippet } from 'svelte';
    import { Checkbox } from '$comp/ui/checkbox';
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
    <Checkbox {id} bind:checked on:click {disabled} />
    <Label
        for={id}
        class="text-sm font-medium leading-none peer-disabled:cursor-not-allowed peer-disabled:opacity-70 peer-data-[disabled=true]:cursor-not-allowed peer-data-[disabled=true]:opacity-70"
    >
        {@render children()}
    </Label>
</div>
