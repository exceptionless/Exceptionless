<script lang="ts">
    import type { Snippet } from 'svelte';

    import { Checkbox } from '$comp/ui/checkbox';
    import Label from '$comp/ui/label/label.svelte';

    interface Props {
        checked?: boolean;
        children: Snippet;
        disabled: boolean;
        id: string;
    }

    let { checked = $bindable(), children, disabled = false, id }: Props = $props();
</script>

<div class="flex items-center space-x-2">
    <Checkbox bind:checked {disabled} {id} />
    <Label
        class="text-sm font-medium leading-none peer-disabled:cursor-not-allowed peer-disabled:opacity-70 peer-data-[disabled=true]:cursor-not-allowed peer-data-[disabled=true]:opacity-70"
        for={id}
    >
        {@render children()}
    </Label>
</div>
