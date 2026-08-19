import { CachedPersistedState } from '$features/shared/utils/cached-persisted-state.svelte';

const authSerializer = {
    deserialize: (value: null | string): null | string => {
        return value === '' ? null : value;
    },
    serialize: (value: null | string): string => {
        return value === null ? '' : value;
    }
};

export const accessToken = new CachedPersistedState<null | string>('satellizer_token', null, {
    serializer: authSerializer
});
