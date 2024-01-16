import { StackStatus } from '$lib/models/api.generated';
import {
	QuestionMarkCircled,
	Circle,
	Stopwatch,
	CheckCircled,
	CrossCircled
} from 'radix-icons-svelte';

export const statuses = [
	{
		value: StackStatus.Open,
		label: 'Open',
		icon: QuestionMarkCircled
	},
	{
		value: StackStatus.Fixed,
		label: 'Fixed',
		icon: Circle
	},
	{
		value: StackStatus.Regressed,
		label: 'Regressed',
		icon: Stopwatch
	},
	{
		value: StackStatus.Snoozed,
		label: 'Snoozed',
		icon: CheckCircled
	},
	{
		value: StackStatus.Ignored,
		label: 'Ignored',
		icon: CrossCircled
	},
	{
		value: StackStatus.Discarded,
		label: 'Discarded',
		icon: CrossCircled
	}
];
