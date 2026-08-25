import { includeIgnoreFile } from '@eslint/compat';
import js from '@eslint/js';
import stylistic from '@stylistic/eslint-plugin';
import pluginQuery from '@tanstack/eslint-plugin-query';
import prettier from 'eslint-config-prettier';
import perfectionist from 'eslint-plugin-perfectionist';
import storybook from 'eslint-plugin-storybook';
import svelte from 'eslint-plugin-svelte';
import globals from 'globals';
import { fileURLToPath } from 'node:url';
import ts from 'typescript-eslint';
const gitignorePath = fileURLToPath(new URL('./.gitignore', import.meta.url));

export default ts.config(
    includeIgnoreFile(gitignorePath),
    js.configs.recommended,
    ...ts.configs.recommended,
    ...svelte.configs['flat/recommended'],
    perfectionist.configs['recommended-natural'],
    prettier,
    ...pluginQuery.configs['flat/recommended'],
    ...svelte.configs['flat/prettier'],
    {
        languageOptions: {
            globals: {
                ...globals.browser,
                ...globals.node
            }
        }
    },
    {
        files: ['**/*.svelte', '**/*.svelte.ts'],
        languageOptions: {
            parserOptions: {
                parser: ts.parser
            }
        }
    },
    {
        ignores: ['.agents/', 'build/', '.svelte-kit/', 'dist/', 'src/lib/generated/', 'src/lib/features/shared/components/ui/']
    },
    {
        rules: {
            '@tanstack/query/exhaustive-deps': 'off'
        }
    },
    {
        files: ['**/*.svelte', '**/*.svelte.ts'],
        plugins: {
            '@stylistic': stylistic
        },
        rules: {
            '@stylistic/brace-style': ['error', '1tbs', { allowSingleLine: false }],
            '@stylistic/object-curly-newline': ['error', { ObjectExpression: { minProperties: 1 } }],
            curly: ['error', 'all'],
            'padding-line-between-statements': ['error', { blankLine: 'always', next: ['if', 'while', 'for', 'do'], prev: 'block-like' }]
        }
    },
    {
        rules: {
            'svelte/no-navigation-without-resolve': 'off'
        }
    },
    {
        rules: {
            'perfectionist/sort-svelte-attributes': 'off'
        }
    },
    {
        files: ['emails/templates/**/*.svelte'],
        rules: {
            'svelte/no-at-html-tags': 'off',
            'svelte/no-useless-mustaches': 'off'
        }
    },
    storybook.configs['flat/recommended']
);
