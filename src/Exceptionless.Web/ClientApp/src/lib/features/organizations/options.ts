import type { DropdownItem } from '$features/shared/options';

import { SuspensionCode } from './models';

export const suspensionCodeOptions: DropdownItem<SuspensionCode>[] = [
    {
        label: 'Billing',
        value: SuspensionCode.Billing
    },
    {
        label: 'Overage',
        value: SuspensionCode.Overage
    },
    {
        label: 'Abuse',
        value: SuspensionCode.Abuse
    },
    {
        label: 'Other',
        value: SuspensionCode.Other
    }
];
