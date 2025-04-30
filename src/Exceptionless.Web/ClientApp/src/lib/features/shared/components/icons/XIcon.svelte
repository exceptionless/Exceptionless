<script lang="ts">
    import type { HTMLAttributes } from 'svelte/elements';

    type MicrosoftIconProps = HTMLAttributes<SVGSVGElement> & {
        class?: string;
        fill?: string;
        height?: number;
        stroke?: string;
        width?: number;
    };

    const { class: className = '', fill = 'currentColor', height = 16, stroke = '', width = 16, ...rest }: MicrosoftIconProps = $props();
</script>

<svg xmlns="http://www.w3.org/2000/svg" viewBox={`0 0 ${width} ${height}`} {width} {height} {fill} {stroke} class={className} aria-label="X icon" {...rest}>
    <path
        d="M12.6.75h2.454l-5.36 6.142L16 15.25h-4.937l-3.867-5.07-4.425 5.07H.316l5.733-6.57L0 .75h5.063l3.495 4.633L12.601.75Zm-.86 13.028h1.36L4.323 2.145H2.865z"
    />
</svg>
