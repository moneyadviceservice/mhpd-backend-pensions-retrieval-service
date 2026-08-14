import { z } from 'zod';

const peiDataSchema = z.object({
  pei: z.string(),
  description: z.string(),
  retrievalStatus: z.string(),
  retrievalRequestedTimestamp: z.string(),
});

export const PensionsRetrievalRecordsSchema = z.object({
  userSessionId: z.string(),
  iss: z.string(),
  jobStartTimestamp: z.string(),
  peisId: z.string(),
  peiRetrievalComplete: z.boolean(),
  peiData: z.array(peiDataSchema),
});

export type PensionsRetrievalRecords = z.infer<typeof PensionsRetrievalRecordsSchema>;
