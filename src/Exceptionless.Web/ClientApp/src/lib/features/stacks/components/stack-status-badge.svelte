<script lang="ts">
    import type { WithElementRef } from 'bits-ui';
    import type { HTMLAnchorAttributes } from 'svelte/elements';

    import { Badge, type BadgeVariant } from '$comp/ui/badge';

    import { StackStatus } from '../models';
    import { stackStatuses } from '../options';

    interface Props extends WithElementRef<HTMLAnchorAttributes> {
        status: StackStatus;
    }

    let { status, ...props }: Props = $props();

    function getVariant(status: StackStatus): BadgeVariant {
        switch (status) {
            case StackStatus.Discarded:
                return 'outline';
            case StackStatus.Fixed:
                return 'default';
            case StackStatus.Ignored:
                return 'outline';
            case StackStatus.Open:
                return 'default';
            case StackStatus.Regressed:
                return 'destructive';
            case StackStatus.Snoozed:
                return 'outline';
            default:
                return 'default';
        }
    }

    const variant: BadgeVariant = $derived(getVariant(status));
    const label = $derived(stackStatuses.find((option) => option.value === status)?.label ?? status);
</script>

<Badge {variant} {...props}>
    {label}
</Badge>
