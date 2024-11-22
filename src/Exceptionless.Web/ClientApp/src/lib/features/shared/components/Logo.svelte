<script lang="ts">
    import type { HTMLAttributes } from 'svelte/elements';

    import logoDark from '$lib/assets/logo-dark.svg';
    import logo from '$lib/assets/logo.svg';
    import { cn } from '$lib/utils';

    let { class: className, ...props }: HTMLAttributes<HTMLImageElement> = $props();
</script>

<img alt="Exceptionless Logo" class={cn('"mx-auto dark:hidden" h-[100px]', className)} src={logo} {...props} />
<img alt="Exceptionless Logo" class={cn('"mx-auto dark:block" hidden h-[100px]', className)} src={logoDark} {...props} />
