<script lang="ts">
    import type { HTMLAttributes } from 'svelte/elements';

    type FacebookIconProps = HTMLAttributes<SVGSVGElement> & {
        class?: string;
        height?: number;
        width?: number;
    };

    const { class: className = '', height = 24, width = 24, ...rest }: FacebookIconProps = $props();
</script>

<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 36 36" {width} {height} class={className} aria-label="Facebook icon" {...rest}>
    <path fill="#1877F2" d="M15 35.8C6.5 34.3 0 26.9 0 18 0 8.1 8.1 0 18 0s18 8.1 18 18c0 8.9-6.5 16.3-15 17.8l-1-.8h-4z" />
    <path fill="#FFF" d="m25 23 .8-5H21v-3.5c0-1.4.5-2.5 2.7-2.5H26V7.4c-1.3-.2-2.7-.4-4-.4-4.1 0-7 2.5-7 7v4h-4.5v5H15v12.7q1.5.3 3 .3c1.5 0 2-.1 3-.3V23z" />
</svg>
