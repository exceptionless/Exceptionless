<script lang="ts">
    import type { HTMLAttributes } from 'svelte/elements';

    type AiSparkleIconProps = HTMLAttributes<SVGSVGElement> & {
        class?: string;
        fill?: string;
        height?: number;
        width?: number;
    };

    const { class: className = '', fill = 'currentColor', height = 24, width = 24, ...rest }: AiSparkleIconProps = $props();
</script>

<!-- The four-point sparkle from the Exceptionless logo's i-dot and app icon; it marks Exie and other AI features. -->
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" {width} {height} class={className} aria-hidden="true" {...rest}>
    <path {fill} d="M12 1C12.94 8.26 15.74 11.06 23 12C15.74 12.94 12.94 15.74 12 23C11.06 15.74 8.26 12.94 1 12C8.26 11.06 11.06 8.26 12 1Z" />
</svg>
