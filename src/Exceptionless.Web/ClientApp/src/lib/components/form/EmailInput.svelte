<script lang="ts">
    import Input from '$comp/ui/input/input.svelte';
    import Label from '$comp/ui/label/label.svelte';
    import type { ProblemDetails } from '@exceptionless/fetchclient';

    export let name: string;
    export let value: unknown;
    export let problem: ProblemDetails | null = null;
    export let required: boolean = false;

    export let autocomplete: string | null = null;
    export let label: string | null = null;
    export let placeholder: string | null = 'Enter email address';

    $: error = problem?.errors?.[name];
    $: label = label ?? name.charAt(0).toUpperCase() + name.slice(1);

    function clearError() {
        problem = problem?.clear(name) || null;
    }
</script>

<div class="space-y-2">
    <Label for={name} class={error ? 'text-destructive' : ''}>
        {label}
        <slot name="label" />
    </Label>
    <Input id={name} type="email" {autocomplete} {placeholder} class="w-full" on:change={clearError} bind:value {required} />
    {#if error}
        <Label for={name} class="text-[0.8rem] font-medium text-destructive">{error.join(' ')}</Label>
    {/if}
</div>
