import { test, expect } from '../lib/test.lib';
import { v4 as uuid } from 'uuid';
import { PensionsRetrievalRecordsSchema } from 'schemas/pensionsRetrievalRecords.schema';
import { setupAndVerifyReady } from 'utilities/helpers';

const iss = 'some-iss';

test.describe('GET - /pension-retrieval-records', () => {
  test('should return 200 with successful request', async ({
    pensionRetrievalService,
    pensionsDataService,
  }) => {
    const services = { pensionRetrievalService, pensionsDataService };
    const sessionId = uuid();
    await setupAndVerifyReady(services, sessionId, iss);

    const response = await pensionRetrievalService.getPensionsRetrievalRecords({
      userSessionId: sessionId,
      mhpdCorrelationId: sessionId,
    });

    expect(response.status).toBe(200);

    const validation = PensionsRetrievalRecordsSchema.safeParse(response.data);

    if (!validation.success) {
      console.error(
        '❌ Pension Retrieval Records Schema Validation Failed:',
        JSON.stringify(validation.error.issues, null, 2),
      );
    }

    expect(validation.success).toBe(true);
  });

  test('should return 200 with missing correlation id', async ({
    pensionRetrievalService,
    pensionsDataService,
  }) => {
    const services = { pensionRetrievalService, pensionsDataService };
    const validSessionId = uuid();
    await setupAndVerifyReady(services, validSessionId, iss);

    const response = await pensionRetrievalService.getPensionsRetrievalRecords({
      userSessionId: validSessionId,
      mhpdCorrelationId: '',
    });

    expect(response.status).toBe(200);
  });

  test('should return 400 with invalid correlation id', async ({
    pensionRetrievalService,
    pensionsDataService,
  }) => {
    const services = { pensionRetrievalService, pensionsDataService };
    const validSessionId = uuid();
    await setupAndVerifyReady(services, validSessionId, iss);

    const response = await pensionRetrievalService.getPensionsRetrievalRecords({
      userSessionId: validSessionId,
      mhpdCorrelationId: 'invalid',
    });

    expect(response.status).toBe(400);
  });

  test('should return 400 with missing user session id', async ({
    pensionRetrievalService,
    pensionsDataService,
  }) => {
    const services = { pensionRetrievalService, pensionsDataService };
    const validSessionId = uuid();

    await setupAndVerifyReady(services, validSessionId, iss);

    const response = await pensionRetrievalService.getPensionsRetrievalRecords({
      userSessionId: '',
      mhpdCorrelationId: validSessionId,
    });

    expect(response.status).toBe(400);
  });

  test('should return 400 with invalid user session id', async ({
    pensionRetrievalService,
    pensionsDataService,
  }) => {
    const services = { pensionRetrievalService, pensionsDataService };
    const validSessionId = uuid();
    await setupAndVerifyReady(services, validSessionId, iss);

    const response = await pensionRetrievalService.getPensionsRetrievalRecords({
      userSessionId: 'invalidid',
      mhpdCorrelationId: validSessionId,
    });

    expect(response.status).toBe(400);
  });
});

test.describe('DELETE - /pension-retrieval-records', () => {
  test('should return 200 with successful request', async ({
    pensionRetrievalService,
    pensionsDataService,
  }) => {
    const services = { pensionRetrievalService, pensionsDataService };
    const sessionId = uuid();
    await setupAndVerifyReady(services, sessionId, iss);

    const response = await pensionRetrievalService.deletePensionsRetrievalRecords({
      userSessionId: sessionId,
      mhpdCorrelationId: sessionId,
    });

    expect(response.status).toBe(200);
  });

  test('should return 200 with missing correlation id', async ({
    pensionRetrievalService,
    pensionsDataService,
  }) => {
    const services = { pensionRetrievalService, pensionsDataService };
    const validSessionId = uuid();
    await setupAndVerifyReady(services, validSessionId, iss);

    const response = await pensionRetrievalService.deletePensionsRetrievalRecords({
      userSessionId: validSessionId,
      mhpdCorrelationId: '',
    });

    expect(response.status).toBe(200);
  });

  test('should return 400 with invalid correlation id', async ({
    pensionRetrievalService,
    pensionsDataService,
  }) => {
    const services = { pensionRetrievalService, pensionsDataService };
    const validSessionId = uuid();
    await setupAndVerifyReady(services, validSessionId, iss);

    const response = await pensionRetrievalService.deletePensionsRetrievalRecords({
      userSessionId: validSessionId,
      mhpdCorrelationId: 'invalid',
    });

    expect(response.status).toBe(400);
  });

  test('should return 400 with missing user session id', async ({
    pensionRetrievalService,
    pensionsDataService,
  }) => {
    const services = { pensionRetrievalService, pensionsDataService };
    const validSessionId = uuid();
    await setupAndVerifyReady(services, validSessionId, iss);

    const response = await pensionRetrievalService.deletePensionsRetrievalRecords({
      userSessionId: '',
      mhpdCorrelationId: validSessionId,
    });

    expect(response.status).toBe(400);
  });

  test('should return 400 with invalid user session id', async ({
    pensionRetrievalService,
    pensionsDataService,
  }) => {
    const services = { pensionRetrievalService, pensionsDataService };
    const validSessionId = uuid();
    await setupAndVerifyReady(services, validSessionId, iss);

    const response = await pensionRetrievalService.deletePensionsRetrievalRecords({
      userSessionId: 'invalidid',
      mhpdCorrelationId: validSessionId,
    });

    expect(response.status).toBe(400);
  });
});
