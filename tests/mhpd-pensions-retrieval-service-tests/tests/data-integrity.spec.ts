import { test, expect } from '../lib/test.lib';
import { v4 as uuid } from 'uuid';
import { setupAndVerifyReady, pollForPensionRecord } from 'utilities/helpers';

test('Lifecycle Integrity: Record deletion and job re-generation', async ({
  pensionRetrievalService,
  pensionsDataService,
}) => {
  const sessionId = uuid();
  const localIss = 'mhpdIss';
  const headers = { userSessionId: sessionId, mhpdCorrelationId: sessionId, iss: localIss };

  await setupAndVerifyReady({ pensionsDataService, pensionRetrievalService }, sessionId, localIss);

  const firstRes = await pensionRetrievalService.getPensionsRetrievalRecords(headers);

  const originalPeisId = firstRes.data?.peisId;
  const originalCount = firstRes.data?.peiData.length;

  await pensionRetrievalService.deletePensionsRetrievalRecords(headers);

  const postDeleteRes = await pensionRetrievalService.getPensionsRetrievalRecords(headers);
  expect(postDeleteRes.data).toBeFalsy();

  await setupAndVerifyReady({ pensionsDataService, pensionRetrievalService }, sessionId, localIss);

  const secondRes = await pollForPensionRecord(pensionRetrievalService, headers, 15, 2000);

  const data2 = secondRes.data;

  if (data2 && firstRes.data) {
    expect(data2.peisId).not.toBe(originalPeisId);
    expect(data2.peiData.length).toBe(originalCount);
  } else {
    throw new Error('Test failed: Pension record data was missing after polling');
  }
});
