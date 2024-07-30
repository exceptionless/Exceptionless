<script lang="ts">
    import type { ProblemDetails } from '@exceptionless/fetchclient';
    import type { Snippet } from 'svelte';

    import Input from '$comp/ui/input/input.svelte';
    import { Label } from '$comp/ui/label';

    interface Props {
        autocomplete?: null | string;
        label?: string | undefined;
        labelChildren?: Snippet;
        maxlength?: number | undefined;
        minlength?: number | undefined;
        name: string;
        placeholder?: string | undefined;
        problem?: null | ProblemDetails;
        required?: boolean;
        value: unknown;
    }

    let {
        autocomplete = null,
        label,
        labelChildren,
        maxlength,
        minlength,
        name,
        placeholder = 'Enter email address',
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
    <Input {autocomplete} bind:value class="w-full" id={name} {maxlength} {minlength} on:change={clearError} {placeholder} {required} type="text" />
    {#if error}
        <Label class="text-[0.8rem] font-medium text-destructive" for={name}>{error.join(' ')}</Label>
    {/if}
</div>
