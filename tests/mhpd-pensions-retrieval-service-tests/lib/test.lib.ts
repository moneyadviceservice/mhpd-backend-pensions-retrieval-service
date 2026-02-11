import { test as baseTest, expect as baseExpect } from '@playwright/test';
import { PensionRetrievalService } from '../services/pensions-retrieval-service';
import { PensionsDataService } from '@services/pensions-data-service';

interface TestFixtures {
  pensionRetrievalService: PensionRetrievalService;
  pensionsDataService: PensionsDataService;
}

export const test = baseTest.extend<TestFixtures>({
  pensionRetrievalService: async ({ request }, use) => {
    const pensionRetrievalService = new PensionRetrievalService(request);
    await use(pensionRetrievalService);
  },

  pensionsDataService: async ({ request }, use) => {
    const pensionsDataService = new PensionsDataService(request);
    await use(pensionsDataService);
  },
});

export const expect = baseExpect;
