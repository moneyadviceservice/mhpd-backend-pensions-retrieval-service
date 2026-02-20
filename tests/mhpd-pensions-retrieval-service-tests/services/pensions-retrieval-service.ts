import { APIClient } from '../lib/api.lib';
import { type APIRequestContext } from '@playwright/test';
import { PensionsRetrievalRecords } from 'schemas/pensionsRetrievalRecords.schema';
import { env } from '@lib/env.lib';

interface PensionsRetrievalRecordsHeaders {
  userSessionId: string;
  mhpdCorrelationId: string;
}

export class PensionRetrievalService {
  protected readonly apiClient: APIClient;
  protected readonly baseURL = env.BASE_URL;

  constructor(request: APIRequestContext) {
    this.apiClient = new APIClient(request, this.baseURL);
  }

  async getPensionsRetrievalRecords(headers: PensionsRetrievalRecordsHeaders) {
    return this.apiClient.get<PensionsRetrievalRecords>('/pensions-retrieval-records', {
      headers: headers as unknown as Record<string, string>,
    });
  }

  async deletePensionsRetrievalRecords(headers: PensionsRetrievalRecordsHeaders) {
    return this.apiClient.delete('/pensions-retrieval-records', {
      headers: headers as unknown as Record<string, string>,
    });
  }
}
