import eslint from '@eslint/js';
import prettier from 'eslint-config-prettier';
import perfectionist from 'eslint-plugin-perfectionist';
import svelte from 'eslint-plugin-svelte';
import globals from 'globals';
import tseslint from 'typescript-eslint';

/** @type {import('eslint').Linter.FlatConfig[]} */
export default tseslint.config(
    eslint.configs.recommended,
    ...tseslint.configs.recommended,
    ...svelte.configs['flat/recommended'],
    perfectionist.configs['recommended-natural'],
    prettier,
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
        files: ['**/*.svelte'],
        languageOptions: {
            parserOptions: {
                parser: tseslint.parser
            }
        }
    },
    {
        ignores: ['build/', '.svelte-kit/', 'dist/', 'src/lib/generated/api.ts', 'src/lib/features/shared/components/ui/']
    },

    {
        /* location of your components where you would like to apply these rules  */
        files: ['src/lib/features/shared/components/ui/**/*.svelte'],
        rules: {
            '@typescript-eslint/no-unused-vars': [
                'warn',
                {
                    argsIgnorePattern: '^_',
                    varsIgnorePattern: '^$$(Props|Events|Slots|Generic)$'
                }
            ]
        }
    },
    {
        rules: {
            'perfectionist/sort-svelte-attributes': 'off'
        }
    }
);
