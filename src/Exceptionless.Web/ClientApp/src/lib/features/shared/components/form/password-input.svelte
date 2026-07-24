<script lang="ts">
    import type { ProblemDetails } from '@exceptionless/fetchclient';
    import type { Snippet } from 'svelte';
    import type { FullAutoFill } from 'svelte/elements';

    import Input from '$comp/ui/input/input.svelte';
    import { Label } from '$comp/ui/label';

    interface Props {
        autocomplete?: FullAutoFill | null | undefined;
        label?: null | string;
        labelChildren?: Snippet;
        maxlength?: number;
        minlength?: number;
        name: string;
        placeholder?: null | string;
        problem?: null | ProblemDetails;
        required?: boolean;
        value: unknown;
    }

    let {
        autocomplete = null,
        label = null,
        labelChildren,
        maxlength,
        minlength,
        name,
        placeholder = 'Enter password',
        problem = null,
        required = false,
        value = $bindable()
    }: Props = $props();
    let error = $derived(problem?.errors?.[name]);

    function clearError() {
        problem = problem?.clear(name) || null;
    }
</script>

<div class="space-y-2">
    <Label class={error ? 'text-destructive' : ''} for={name}>
        {label ?? name.charAt(0).toUpperCase() + name.slice(1)}
        {#if labelChildren}
            {@render labelChildren()}
        {/if}
    </Label>
    <Input {autocomplete} bind:value class="w-full" id={name} {maxlength} {minlength} onchange={clearError} {placeholder} {required} type="password" />
    {#if error}
        <Label class="text-destructive text-[0.8rem] font-medium" for={name}>{error.join(' ')}</Label>
    {/if}
</div>
