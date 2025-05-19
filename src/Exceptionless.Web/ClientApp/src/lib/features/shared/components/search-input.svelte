<script lang="ts">
    import type { HTMLInputAttributes, HTMLInputTypeAttribute } from 'svelte/elements';

    import { Input } from '$comp/ui/input';
    import { cn, type WithElementRef } from '$lib/utils.js';
    import Search from '@lucide/svelte/icons/search';

    type InputType = Exclude<HTMLInputTypeAttribute, 'file'>;

    type Props = WithElementRef<Omit<HTMLInputAttributes, 'type'> & ({ files?: FileList; type: 'file' } | { files?: undefined; type?: InputType })>;

    let { class: className, id = 'search', placeholder = 'Search...', value = $bindable(), ...props }: Props = $props();
</script>

<div class="relative">
    <label class="sr-only" for={id}>Search</label>
    <div class="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-3">
        <Search class="h-5 w-5" />
    </div>
    <Input bind:value class={cn('pl-10', className)} {id} name="search" {placeholder} type="text" {...props} />
</div>
