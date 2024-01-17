import { StackStatus } from '$lib/models/api.generated';

export const statuses = [
	{
		value: StackStatus.Open,
		label: 'Open'
	},
	{
		value: StackStatus.Fixed,
		label: 'Fixed'
	},
	{
		value: StackStatus.Regressed,
		label: 'Regressed'
	},
	{
		value: StackStatus.Snoozed,
		label: 'Snoozed'
	},
	{
		value: StackStatus.Ignored,
		label: 'Ignored'
	},
	{
		value: StackStatus.Discarded,
		label: 'Discarded'
	}
];
