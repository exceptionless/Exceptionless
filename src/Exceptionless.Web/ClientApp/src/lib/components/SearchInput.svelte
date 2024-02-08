<script lang="ts">
    import type { HTMLInputAttributes } from 'svelte/elements';
    import IconSearch from '~icons/mdi/search';

    import { Input } from '$comp/ui/input';
    import { cn } from '$lib/utils';

    type $$Props = HTMLInputAttributes;

    let className: $$Props['class'] = undefined;
    export { className as class };
    export let value: $$Props['value'] = undefined;

    export let id: string | null | undefined = 'search';
    export let placeholder: string | null | undefined = 'Search...';
</script>

<div class="relative">
    <label for={id} class="sr-only">Search</label>
    <div class="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-3">
        <IconSearch class="h-5 w-5" />
    </div>
    <Input
        type="text"
        name="search"
        {id}
        class={cn('pl-10', className)}
        bind:value
        on:blur
        on:change
        on:click
        on:focus
        on:focusin
        on:focusout
        on:keydown
        on:keypress
        on:keyup
        on:mouseover
        on:mouseenter
        on:mouseleave
        on:paste
        on:input
        {placeholder}
        {...$$restProps}
    />
</div>
