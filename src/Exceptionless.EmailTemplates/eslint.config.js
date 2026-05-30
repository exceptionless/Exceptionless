import js from '@eslint/js';
import svelte from 'eslint-plugin-svelte';
import tsParser from '@typescript-eslint/parser';
import prettierConfig from 'eslint-config-prettier';
import globals from 'globals';

/** @type {import('eslint').Linter.Config[]} */
export default [
    js.configs.recommended,
    ...svelte.configs['flat/recommended'],
    prettierConfig,
    {
        languageOptions: {
            globals: {
                ...globals.browser,
                ...globals.node
            }
        }
    },
    {
        files: ['**/*.svelte'],
        languageOptions: {
            parserOptions: {
                parser: tsParser
            }
        }
    },
    {
        // Templates use {@html '{{#if ...}}'} to pass Handlebars block syntax through Svelte.
        // This is intentional — disable the no-at-html-tags rule for templates only.
        files: ['src/templates/**/*.svelte'],
        rules: {
            'svelte/no-at-html-tags': 'off'
        }
    },
    {
        ignores: ['dist/**', 'node_modules/**', '.storybook/**', 'storybook-static/**']
    }
];
