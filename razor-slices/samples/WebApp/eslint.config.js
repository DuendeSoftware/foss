import js from '@eslint/js';
import { defineConfig } from 'eslint/config';

export default defineConfig([
  {
    ignores: ['wwwroot/lib/**', 'bin/**', 'obj/**']
  },
  {
    files: ['Build/**/*.js', 'wwwroot/js/**/*.js', 'eslint.config.js'],
    extends: [js.configs.recommended],
    rules: {
      'no-empty': ['error', { allowEmptyCatch: true }]
    }
  },
  {
    files: ['Build/**/*.js'],
    languageOptions: {
      globals: {
        console: 'readonly'
      }
    }
  }
]);
