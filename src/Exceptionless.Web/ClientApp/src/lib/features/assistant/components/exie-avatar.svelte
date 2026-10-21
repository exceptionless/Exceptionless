<script lang="ts">
    import AiSparkle from '#comp/icons/AiSparkleIcon.svelte';
    import { cn } from '#lib/utils.ts';

    interface Props {
        class?: string;
        size?: 'lg' | 'md' | 'sm';
    }

    let { class: className, size = 'md' }: Props = $props();
</script>

<span
    aria-hidden="true"
    class={cn(
        'from-primary/20 to-primary/5 text-primary ring-primary/15 flex shrink-0 items-center justify-center bg-linear-to-br ring-1 ring-inset',
        size === 'sm' && 'size-6 rounded-md [&_svg]:size-3.5',
        size === 'md' && 'size-8 rounded-lg [&_svg]:size-4',
        size === 'lg' && 'size-12 rounded-xl [&_svg]:size-6',
        className
    )}
>
    <AiSparkle />
</span>
