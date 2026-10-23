import { defineEnvVars } from '@sveltejs/kit/env';

// Public variables are dynamic: the container entrypoint (build/update-config.sh) rewrites
// _app/env.js with runtime settings, and the browser reads them without validation.
// Keep every value a plain string so build-time and container values have the same shape.
const optional = (value: string | undefined) => value;

export const variables = defineEnvVars({
    PUBLIC_APP_VERSION: { public: true, schema: optional },
    PUBLIC_BASE_URL: { description: 'API origin; empty uses the UI origin.', public: true },
    PUBLIC_ENABLE_ACCOUNT_CREATION: { public: true },
    PUBLIC_ENABLE_SSL: { public: true, schema: optional },
    PUBLIC_EXCEPTIONLESS_API_KEY: { description: 'API key the UI uses to report its own errors.', public: true },
    PUBLIC_EXCEPTIONLESS_CLIENT_SETUP_SHOW_SERVER_URL: { public: true },
    PUBLIC_EXCEPTIONLESS_SERVER_URL: { description: 'Server URL shown in client setup instructions and used for UI telemetry.', public: true },
    PUBLIC_EXCEPTIONLESS_TELEMETRY_SERVER_URL: { public: true, schema: optional },
    PUBLIC_GITHUB_APPID: { public: true },
    PUBLIC_GOOGLE_APPID: { public: true },
    PUBLIC_INTERCOM_APPID: { public: true },
    PUBLIC_MICROSOFT_APPID: { public: true },
    PUBLIC_SLACK_APPID: { public: true },
    PUBLIC_STRIPE_PUBLISHABLE_KEY: { public: true },
    PUBLIC_SYSTEM_NOTIFICATION_MESSAGE: { public: true }
});
