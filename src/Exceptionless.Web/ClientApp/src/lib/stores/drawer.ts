import type { ComponentType } from "svelte";
import { writable, type Writable } from "svelte/store";

export const showDrawer = writable(false);
export const drawerComponent: Writable<ComponentType | null> = writable(null);
