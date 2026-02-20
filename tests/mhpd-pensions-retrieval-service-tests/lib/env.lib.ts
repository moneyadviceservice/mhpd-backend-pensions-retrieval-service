import { z } from 'zod';
import { configDotenv } from 'dotenv';

configDotenv({ quiet: true });

const envSchema = z.object({
  BASE_URL: z.string(),
  BASE_URL_PDS: z.string(),
  CLIENT_ID: z.string(),
  CLIENT_SECRET: z.string(),
  AUTHORISATION_CODE: z.string(),
  REDIRECT_URL: z.string(),
  CODE_VERIFIER: z.string(),
  TICKET: z.string(),
  CI: z.any().optional(),
});

type Env = z.infer<typeof envSchema>;

// This should be the only place where this rule is disabled
// eslint-disable-next-line no-restricted-properties
export const env: Env = envSchema.parse(process.env);
