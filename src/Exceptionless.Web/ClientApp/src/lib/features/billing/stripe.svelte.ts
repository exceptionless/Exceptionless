/**
 * Stripe context and hooks for billing integration.
 *
 * Provides lazy-loaded Stripe instance with context-based access following
 * the svelte-intercom provider pattern.
 */

import type { Stripe, StripeElements } from '@stripe/stripe-js';

import { env } from '$env/dynamic/public';
import { loadStripe } from '@stripe/stripe-js';
import { getContext, setContext } from 'svelte';

const STRIPE_CONTEXT_KEY = Symbol('stripe-context');

export interface StripeContext {
    readonly elements: null | StripeElements;
    readonly error: null | string;
    readonly isLoading: boolean;
    readonly stripe: null | Stripe;
}

/**
 * Get the Stripe publishable key from environment.
 */
export function getStripePublishableKey(): string | undefined {
    return env.PUBLIC_STRIPE_PUBLISHABLE_KEY;
}

/**
 * Check if Stripe is enabled via environment configuration.
 */
export function isStripeEnabled(): boolean {
    return !!env.PUBLIC_STRIPE_PUBLISHABLE_KEY;
}

// Singleton to prevent multiple Stripe loads
let _stripePromise: null | Promise<null | Stripe> = null;
let _stripeInstance: null | Stripe = null;

/**
 * Load Stripe instance lazily. Returns cached instance if already loaded.
 * Resets on failure so subsequent calls can retry (e.g. after a transient network error).
 */
export async function loadStripeOnce(): Promise<null | Stripe> {
    if (_stripeInstance) {
        return _stripeInstance;
    }

    if (!isStripeEnabled()) {
        return null;
    }

    if (!_stripePromise) {
        _stripePromise = loadStripe(env.PUBLIC_STRIPE_PUBLISHABLE_KEY!);
    }

    try {
        _stripeInstance = await _stripePromise;

        if (!_stripeInstance) {
            _stripePromise = null;
        }

        return _stripeInstance;
    } catch (error: unknown) {
        // Reset so the next call can retry instead of re-awaiting the rejected promise
        _stripePromise = null;
        _stripeInstance = null;
        throw error;
    }
}

/**
 * Set the Stripe context. Called by StripeProvider.
 */
export function setStripeContext(ctx: StripeContext): void {
    setContext(STRIPE_CONTEXT_KEY, ctx);
}

/**
 * Try to get the Stripe context without throwing.
 * Returns null if not within a StripeProvider.
 */
export function tryUseStripe(): null | StripeContext {
    return getContext<StripeContext | undefined>(STRIPE_CONTEXT_KEY) ?? null;
}

/**
 * Get the Stripe context. Must be called within a StripeProvider.
 * @throws Error if called outside of StripeProvider
 */
export function useStripe(): StripeContext {
    const ctx = getContext<StripeContext | undefined>(STRIPE_CONTEXT_KEY);
    if (!ctx) {
        throw new Error('useStripe() must be called within a StripeProvider component');
    }

    return ctx;
}
