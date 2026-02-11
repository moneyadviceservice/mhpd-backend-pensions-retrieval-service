import * as eslint from '@eslint/js';
import tseslint from 'typescript-eslint';
import { defineConfig } from 'eslint/config';

export default defineConfig([
  eslint.configs.recommended,
  tseslint.configs.recommended,
  tseslint.configs.strictTypeChecked,
  tseslint.configs.stylisticTypeChecked,
  {
    ignores: ['**/*.mjs'],
  },
  {
    languageOptions: { parserOptions: { projectService: true } },
  },
  {
    files: ['**/*.{ts,mts,cts}'],
    rules: {
      '@typescript-eslint/non-nullable-type-assertion-style': 'off',
      '@typescript-eslint/no-floating-promises': 'error',
      'no-restricted-properties': [
        'error',
        {
          object: 'process',
          property: 'env',
          message: 'Do not use process.env, please import any environment variable using env.lib.ts',
        },
      ],
    },
  },
]);