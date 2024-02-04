<script lang="ts">
    import { cn } from '$lib/utils';

    let className: string | undefined | null = undefined;
    export { className as class };
</script>

<div class={cn('text-lg font-semibold', className)} {...$$restProps}>
    <slot />
</div>
