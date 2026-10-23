<script lang="ts">
    import type { HTMLAttributes } from 'svelte/elements';

    type ExieIconProps = HTMLAttributes<SVGSVGElement> & {
        class?: string;
        height?: number;
        width?: number;
    };

    const { class: className = '', height = 24, width = 24, ...rest }: ExieIconProps = $props();
</script>

<!-- Exie's robot, with the four-point sparkle from the logo's i-dot as its antenna tip. -->
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" {width} {height} class={className} aria-hidden="true" {...rest}>
    <g fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
        <path d="M12 8V7" />
        <rect width="16" height="12" x="4" y="8" rx="2" />
        <path d="M2 14h2" />
        <path d="M20 14h2" />
        <path d="M15 13v2" />
        <path d="M9 13v2" />
    </g>
    <path
        fill="currentColor"
        d="M12 0C12.32 2.48 13.28 3.43 15.75 3.75C13.28 4.07 12.32 5.03 12 7.5C11.68 5.03 10.72 4.07 8.25 3.75C10.72 3.43 11.68 2.48 12 0Z"
    />
</svg>
