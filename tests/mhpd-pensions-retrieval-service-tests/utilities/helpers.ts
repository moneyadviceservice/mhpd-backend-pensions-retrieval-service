import { env } from '../lib/env.lib';
import { PensionRetrievalService } from '../services/pensions-retrieval-service';
import { PensionsDataService, PostPensionsDataHeaders } from '../services/pensions-data-service';

export interface RetrievalHeaders {
  userSessionId: string;
  mhpdCorrelationId: string;
  iss: string;
  'X-XSRF-TOKEN'?: string;
}

export async function pollForPensionRecord(
  service: PensionRetrievalService,
  headers: RetrievalHeaders,
  maxAttempts = 10,
  interval = 2000,
) {
  let attempts = 0;

  while (attempts < maxAttempts) {
    const response = await service.getPensionsRetrievalRecords(headers);

    if (response.status === 200 && response.data?.userSessionId) {
      return response;
    }

    console.log(`[Polling] Record not ready yet. Attempt ${String(attempts + 1)}/${String(maxAttempts)}...`);
    await new Promise((res) => setTimeout(res, interval));
    attempts++;
  }

  throw new Error(`Exceeded max polling attempts for session: ${headers.userSessionId}`);
}

export async function setupAndVerifyReady(
  services: {
    pensionsDataService: PensionsDataService;
    pensionRetrievalService: PensionRetrievalService;
  },
  sessionId: string,
  iss: string,
) {
  const { pensionsDataService, pensionRetrievalService } = services;

  const pdsCsrfResponse = await pensionsDataService.getCSRFToken();
  const pdsCsrfToken = pdsCsrfResponse.cookies.get('X-XSRF-TOKEN');

  if (!pdsCsrfToken) {
    throw new Error('Failed to retrieve CSRF token from PensionsDataService');
  }

  const setupHeaders: RetrievalHeaders = {
    userSessionId: sessionId,
    iss,
    mhpdCorrelationId: sessionId,
    'X-XSRF-TOKEN': pdsCsrfToken,
  };

  const strictHeaders = setupHeaders as PostPensionsDataHeaders;

  await pensionsDataService.postPensionsData(strictHeaders, {
    clientId: env.CLIENT_ID,
    clientSecret: env.CLIENT_SECRET,
    authorisationCode: env.AUTHORISATION_CODE,
    redirectUrl: env.REDIRECT_URL,
    codeVerifier: env.CODE_VERIFIER,
  });

  await pensionsDataService.postPensionsDataRetrieval(strictHeaders, {
    ticket: env.TICKET,
    clientId: env.CLIENT_ID,
  });

  await pollForPensionRecord(pensionRetrievalService, {
    userSessionId: sessionId,
    mhpdCorrelationId: sessionId,
    iss,
  });
}
