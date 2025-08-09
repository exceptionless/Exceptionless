import type { DropdownItem } from '$features/shared/options';

import { StackStatus } from '$features/stacks/models';

export const stackStatuses: DropdownItem<StackStatus>[] = [
    {
        label: 'Open',
        value: StackStatus.Open
    },
    {
        label: 'Fixed',
        value: StackStatus.Fixed
    },
    {
        label: 'Regressed',
        value: StackStatus.Regressed
    },
    {
        label: 'Snoozed',
        value: StackStatus.Snoozed
    },
    {
        label: 'Ignored',
        value: StackStatus.Ignored
    },
    {
        label: 'Discarded',
        value: StackStatus.Discarded
    }
];
