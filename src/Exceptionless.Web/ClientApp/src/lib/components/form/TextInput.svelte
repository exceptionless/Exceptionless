<script lang="ts">
    import type { Snippet } from 'svelte';
    import type { ProblemDetails } from '@exceptionless/fetchclient';

    import Input from '$comp/ui/input/input.svelte';
    import { Label } from '$comp/ui/label';

    interface Props {
        name: string;
        value: unknown;
        problem?: ProblemDetails | null;
        required?: boolean;
        autocomplete?: string | null;
        label?: string | undefined;
        labelChildren?: Snippet;
        minlength?: number | undefined;
        maxlength?: number | undefined;
        placeholder?: string | undefined;
    }

    let {
        name,
        value = $bindable(),
        problem = null,
        required = false,
        autocomplete = null,
        label,
        labelChildren,
        minlength,
        maxlength,
        placeholder = 'Enter email address'
    }: Props = $props();
    let error = $derived(problem?.errors?.[name]);

    function clearError() {
        problem = problem?.clear(name) || null;
    }
</script>

<div class="space-y-2">
    <Label for={name} class={error ? 'text-destructive' : ''}>
        {label ?? name.charAt(0).toUpperCase() + name.slice(1)}
        {#if labelChildren}
            {@render labelChildren()}
        {/if}
    </Label>
    <Input id={name} type="text" {autocomplete} {placeholder} {minlength} {maxlength} class="w-full" on:change={clearError} bind:value {required} />
    {#if error}
        <Label for={name} class="text-[0.8rem] font-medium text-destructive">{error.join(' ')}</Label>
    {/if}
</div>
