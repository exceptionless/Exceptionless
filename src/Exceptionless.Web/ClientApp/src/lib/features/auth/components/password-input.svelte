<script lang="ts">
    import type { HTMLInputAttributes } from 'svelte/elements';

    import Eye from '@lucide/svelte/icons/eye';
    import EyeOff from '@lucide/svelte/icons/eye-off';

    import * as InputGroup from '#comp/ui/input-group/index.ts';

    type Props = Omit<HTMLInputAttributes, 'files' | 'type' | 'value'> & {
        value: string;
    };

    let { value, ...restProps }: Props = $props();

    let visible = $state(false);
</script>

<InputGroup.Root>
    <InputGroup.Input {...restProps} type={visible ? 'text' : 'password'} {value} />
    <InputGroup.Addon align="inline-end">
        <InputGroup.Button
            aria-label={visible ? 'Hide password' : 'Show password'}
            aria-pressed={visible}
            onclick={() => (visible = !visible)}
            size="icon-xs"
            title={visible ? 'Hide password' : 'Show password'}
        >
            {#if visible}
                <EyeOff aria-hidden="true" />
            {:else}
                <Eye aria-hidden="true" />
            {/if}
        </InputGroup.Button>
    </InputGroup.Addon>
</InputGroup.Root>
