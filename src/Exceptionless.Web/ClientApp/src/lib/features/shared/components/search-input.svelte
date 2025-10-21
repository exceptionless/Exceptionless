<script lang="ts">
    import type { HTMLInputAttributes, HTMLInputTypeAttribute } from 'svelte/elements';

    import * as InputGroup from '$comp/ui/input-group';
    import { type WithElementRef } from '$lib/utils';
    import Search from '@lucide/svelte/icons/search';

    type InputType = Exclude<HTMLInputTypeAttribute, 'file'>;

    type BaseProps = WithElementRef<Omit<HTMLInputAttributes, 'type'> & ({ files?: FileList; type: 'file' } | { files?: undefined; type?: InputType })>;

    type Props = BaseProps & {
        groupClass?: string;
    };

    let { class: className, groupClass = '', id = 'search', placeholder = 'Search...', value = $bindable(), ...props }: Props = $props();
</script>

<label class="sr-only" for={id}>Search</label>
<InputGroup.Root class={groupClass}>
    <InputGroup.Addon aria-hidden="true">
        <Search class="size-5" />
    </InputGroup.Addon>
    <InputGroup.Input bind:value class={className} {id} name="search" {placeholder} type="text" {...props} />
</InputGroup.Root>
