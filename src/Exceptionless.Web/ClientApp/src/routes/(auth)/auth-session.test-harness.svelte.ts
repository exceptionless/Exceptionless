/** A reactive stand-in for the persisted access token, so page tests can sign a user out. */
export const testAccessToken = $state<{ current: null | string }>({
    current: null
});
