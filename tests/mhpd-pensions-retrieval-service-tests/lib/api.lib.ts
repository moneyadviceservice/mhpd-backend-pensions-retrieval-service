import { APIRequestContext, APIResponse } from '@playwright/test';
import { test } from './test.lib';

interface BaseOptions {
  headers?: Record<string, string>;
}

interface GetOptions extends BaseOptions {
  params?: Record<string, string>;
}

interface PostOptions extends BaseOptions {
  data?: Record<string, string>;
}

interface DeleteOptions extends BaseOptions {
  params?: Record<string, string>;
}

interface ServiceResponse<T> {
  cookies: Map<string, string>;
  status: number;
  data: T | null;
}

function parseCookies(response: APIResponse) {
  const setCookiesHeader = response
    .headersArray()
    .filter((h) => h.name.toLowerCase() === 'set-cookie')
    .map((h) => h.value);

  const cookies = new Map<string, string>();
  for (const cookie of setCookiesHeader) {
    const [cookiePart] = cookie.split(';');
    const [name, value] = cookiePart.split('=');

    if (name && value) {
      cookies.set(name.trim(), value.trim());
    }
  }

  return cookies;
}

async function safeJson(response: APIResponse) {
  try {
    return (await response.json()) as unknown;
  } catch {
    if (response.headersArray().some((h) => h.name.includes('application/json')))
      throw new Error(
        `Did not get valid JSON response from request (status code: ${response.status().toString()})`,
      );
  }
  return null;
}

export class APIClient {
  constructor(
    readonly request: APIRequestContext,
    readonly baseURL: string,
  ) {}

  async get<T>(endpoint: string, options?: GetOptions): Promise<ServiceResponse<T>> {
    const response = await this.request.get(this.baseURL + endpoint, {
      headers: options?.headers,
      params: options?.params,
    });

    const json = (await safeJson(response)) as T | null;
    const cookies = parseCookies(response);

    await test.info().attach(endpoint, {
      contentType: 'application/json',
      body: JSON.stringify(
        {
          endpoint: this.baseURL + endpoint,
          headers: options?.headers,
          params: options?.params,
          cookies: Object.fromEntries(cookies),
          status: response.status(),
          data: json,
        },
        null,
        4,
      ),
    });

    return {
      cookies,
      status: response.status(),
      data: json,
    };
  }

  async post<T>(endpoint: string, options?: PostOptions): Promise<ServiceResponse<T>> {
    const response = await this.request.post(this.baseURL + endpoint, {
      headers: options?.headers,
      data: options?.data,
    });

    const json = (await safeJson(response)) as T | null;
    const cookies = parseCookies(response);

    await test.info().attach(endpoint, {
      contentType: 'application/json',
      body: JSON.stringify(
        {
          endpoint: this.baseURL + endpoint,
          headers: options?.headers,
          requestData: options?.data,
          cookies: Object.fromEntries(cookies),
          status: response.status(),
          responseData: json,
        },
        null,
        4,
      ),
    });

    return {
      cookies,
      status: response.status(),
      data: json,
    };
  }

  async delete<T>(endpoint: string, options?: DeleteOptions): Promise<ServiceResponse<T>> {
    const response = await this.request.delete(this.baseURL + endpoint, {
      headers: options?.headers,
      params: options?.params,
    });

    const json = (await safeJson(response)) as T | null;
    const cookies = parseCookies(response);

    await test.info().attach(endpoint, {
      contentType: 'application/json',
      body: JSON.stringify(
        {
          endpoint: this.baseURL + endpoint,
          headers: options?.headers,
          params: options?.params,
          cookies: Object.fromEntries(cookies),
          status: response.status(),
          data: json,
        },
        null,
        4,
      ),
    });

    return {
      cookies,
      status: response.status(),
      data: json,
    };
  }
}
