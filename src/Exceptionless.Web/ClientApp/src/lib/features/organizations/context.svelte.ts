import { PersistedState } from 'runed';

export const organization = new PersistedState<string | undefined>('organization', undefined);
