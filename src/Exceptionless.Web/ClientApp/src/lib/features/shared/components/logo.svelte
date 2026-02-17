<script lang="ts">
    import type { HTMLAttributes } from 'svelte/elements';

    import logoDark from '$lib/assets/logo-dark.svg';
    import logo from '$lib/assets/logo.svg';
    import { cn } from '$lib/utils';

    let { class: className, ...props }: HTMLAttributes<HTMLImageElement> = $props();
</script>

<img alt="Exceptionless Logo" class={cn('mx-auto max-h-25 w-auto dark:hidden', className)} src={logo} {...props} />
<img alt="Exceptionless Logo" class={cn('mx-auto hidden max-h-25 w-auto dark:block', className)} src={logoDark} {...props} />
