import type { ComponentType } from 'svelte';
import { writable, type Writable } from 'svelte/store';

export const drawerComponent: Writable<ComponentType | null> = writable(null);
export const drawerComponentProps: Writable<object | null> = writable(null);

export const showDrawer = writable(false);
showDrawer.subscribe((show) => {
	if (!show) {
		drawerComponent.set(null);
		drawerComponentProps.set(null);
	}
});
