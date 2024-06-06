<script lang="ts">
    import type { Snippet } from 'svelte';
    import type { ProblemDetails } from '@exceptionless/fetchclient';

    import Input from '$comp/ui/input/input.svelte';
    import Label from '$comp/ui/label/label.svelte';

    interface Props {
        name: string;
        value: unknown;
        problem?: ProblemDetails | null;
        required?: boolean;
        autocomplete?: string | null;
        label?: string | null;
        labelChildren?: Snippet;
        placeholder?: string | null;
    }

    let {
        name,
        value,
        problem = null,
        required = false,
        autocomplete = null,
        label = null,
        labelChildren,
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
    <Input id={name} type="email" {autocomplete} {placeholder} class="w-full" on:change={clearError} bind:value {required} />
    {#if error}
        <Label for={name} class="text-[0.8rem] font-medium text-destructive">{error.join(' ')}</Label>
    {/if}
</div>
