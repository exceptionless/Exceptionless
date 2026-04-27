// Mock for $env/dynamic/public in Storybook
export const env = {
    // Filter to only include PUBLIC_ prefixed environment variables
    ...Object.fromEntries(Object.entries(import.meta.env).filter(([key]) => key.startsWith('PUBLIC_'))),
    // Provide a Stripe publishable key so isStripeEnabled() returns true in
    // billing stories. The StripeProvider will fail to init (expected).
    PUBLIC_STRIPE_PUBLISHABLE_KEY: import.meta.env.PUBLIC_STRIPE_PUBLISHABLE_KEY || 'pk_test_storybook_placeholder'
};
