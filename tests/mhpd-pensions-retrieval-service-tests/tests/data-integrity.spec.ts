import { test, expect } from '../lib/test.lib';
import { v4 as uuid } from 'uuid';
import { setupAndVerifyReady, pollForPensionRecord } from 'utilities/helpers';

test('Retrieval Integrity: Identity Stability and Timestamp Linearity', async ({
  pensionRetrievalService,
  pensionsDataService,
}) => {
  const sessionId = uuid();
  const localIss = 'mhpdIss';
  const headers = { userSessionId: sessionId, mhpdCorrelationId: sessionId, iss: localIss };

  await setupAndVerifyReady({ pensionsDataService, pensionRetrievalService }, sessionId, localIss);

  const firstRes = await pensionRetrievalService.getPensionsRetrievalRecords(headers);
  const data1 = firstRes.data;

  if (!data1) throw new Error('Data1 is unexpectedly null');

  const secondRes = await pensionRetrievalService.getPensionsRetrievalRecords(headers);
  const data2 = secondRes.data;

  expect(data1.id).toBe(data2?.id);
  expect(data1.peisId).toBe(data2?.peisId);

  const jobStart = new Date(data1.jobStartTimestamp).getTime();

  data1.peiData.forEach((item: { retrievalRequestedTimestamp: string; pei: string }) => {
    const itemTime = new Date(item.retrievalRequestedTimestamp).getTime();
    expect(itemTime).toBeGreaterThanOrEqual(jobStart);

    const peiParts = item.pei.split(':');
    expect(peiParts).toHaveLength(2);
    expect(peiParts[0]).toBeTruthy();
    expect(peiParts[1]).toBeTruthy();
  });
});

test('Lifecycle Integrity: Record deletion and job re-generation', async ({
  pensionRetrievalService,
  pensionsDataService,
}) => {
  const sessionId = uuid();
  const localIss = 'mhpdIss';
  const headers = { userSessionId: sessionId, mhpdCorrelationId: sessionId, iss: localIss };

  await setupAndVerifyReady({ pensionsDataService, pensionRetrievalService }, sessionId, localIss);

  const firstRes = await pensionRetrievalService.getPensionsRetrievalRecords(headers);

  const originalId = firstRes.data?.id;
  const originalPeisId = firstRes.data?.peisId;
  const originalCount = firstRes.data?.peiData.length;

  await pensionRetrievalService.deletePensionsRetrievalRecords(headers);

  const postDeleteRes = await pensionRetrievalService.getPensionsRetrievalRecords(headers);
  expect(postDeleteRes.data).toBeFalsy();

  await setupAndVerifyReady({ pensionsDataService, pensionRetrievalService }, sessionId, localIss);

  const secondRes = await pollForPensionRecord(pensionRetrievalService, headers, 15, 2000);

  const data2 = secondRes.data;

  if (data2 && firstRes.data) {
    expect(data2.id).not.toBe(originalId);
    expect(data2.peisId).not.toBe(originalPeisId);
    expect(data2.peiData.length).toBe(originalCount);
  } else {
    throw new Error('Test failed: Pension record data was missing after polling');
  }
});
