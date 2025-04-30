<script lang="ts">
    import type { HTMLAttributes } from 'svelte/elements';

    type MicrosoftIconProps = HTMLAttributes<SVGSVGElement> & {
        class?: string;
        fill?: string;
        height?: number;
        stroke?: string;
        width?: number;
    };

    const { class: className = '', fill = 'currentColor', height = 24, stroke = '', width = 24, ...rest }: MicrosoftIconProps = $props();
</script>

<svg
    xmlns="http://www.w3.org/2000/svg"
    viewBox={`0 0 ${width} ${height}`}
    {width}
    {height}
    {fill}
    {stroke}
    class={className}
    aria-label="Microsoft icon"
    {...rest}
>
    <path d="M2,3H11V12H2V3M11,22H2V13H11V22M21,3V12H12V3H21M21,22H12V13H21V22Z" />
</svg>
