import { APIClient } from '@lib/api.lib';
import { type APIRequestContext } from '@playwright/test';
import { env } from 'node:process';

export interface PostPensionsDataHeaders {
  userSessionId: string;
  iss: string;
  mhpdCorrelationId: string;
  'X-XSRF-TOKEN': string;
}

interface PostPensionsData {
  clientId: string;
  clientSecret: string;
  authorisationCode: string;
  redirectUrl: string;
  codeVerifier: string;
}

interface PostPensionsDataRetrieval {
  ticket?: string;
  clientId?: string;
}

export class PensionsDataService {
  protected readonly apiClient: APIClient;
  protected readonly baseURL: string = env.BASE_URL_PDS as string;

  constructor(request: APIRequestContext) {
    this.apiClient = new APIClient(request, this.baseURL);
  }

  async postPensionsData(headers: PostPensionsDataHeaders, data: PostPensionsData) {
    return this.apiClient.post('/pensions-data', {
      headers: headers as unknown as Record<string, string>,
      data: data as unknown as Record<string, string>,
    });
  }

  async postPensionsDataRetrieval(headers: PostPensionsDataHeaders, data: PostPensionsDataRetrieval) {
    return this.apiClient.post('/pensions-data-retrieval', {
      headers: headers as unknown as Record<string, string>,
      data: data as unknown as Record<string, string>,
    });
  }

  async getCSRFToken() {
    return this.apiClient.get('/csrf-token');
  }
}
