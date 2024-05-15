<script lang="ts">
    import { cn } from '$lib/utils';
    import { variants, type Props } from './A';

    let { children, class: className, variant = 'default', ...props }: Props = $props();
</script>

<a class={cn(variants({ variant, className }))} {...props}>
    {@render children()}
</a>
