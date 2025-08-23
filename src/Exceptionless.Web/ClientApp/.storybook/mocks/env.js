// Mock for $env/dynamic/public in Storybook
export const env = {
    // Filter to only include PUBLIC_ prefixed environment variables
    ...Object.fromEntries(
        Object.entries(import.meta.env).filter(([key]) => key.startsWith('PUBLIC_'))
    )
};
