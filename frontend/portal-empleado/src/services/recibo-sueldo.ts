export type ReciboItem = {
  id: string;
  periodo: string;
  importe: number;
  moneda: string;
  estado: string;
  fechaEmision: string;
  pdfUrl?: string | null;
};

export type ReciboSueldoResponse = {
  cuil: string;
  items: ReciboItem[];
};

export type ReceiptYearsResponse = {
  years: number[];
};

export type ReceiptMonthsResponse = {
  year: number;
  months: number[];
};

export type ReceiptCacheItem = {
  id: string;
  periodo: string;
  importe: number;
  moneda: string;
  estado: string;
  fechaEmision: string;
  pdfUrl?: string | null;
};

export type ReceiptSnapshotResponse = {
  cuil: string;
  year: number;
  month: number;
  version: number;
  downloadedAtUtc: string;
  sourceKey: string;
  sourceEtag?: string | null;
  sourceVersionId?: string | null;
  status: string;
  item?: ReceiptCacheItem | null;
  payloadJson: string;
};

export type ReceiptRefreshResponse = {
  refreshed: boolean;
  status: string;
  snapshot: ReceiptSnapshotResponse;
};

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? '/api';

export function getReciboPdfUrl(reciboId: string): string {
  return `${API_BASE_URL}/recibo-sueldo/${encodeURIComponent(reciboId)}/pdf`;
}

export async function getReciboSueldo(): Promise<ReciboSueldoResponse> {
  const response = await fetch(`${API_BASE_URL}/recibo-sueldo`, {
    credentials: 'include',
  });

  if (!response.ok) {
    throw await toApiError(response, 'Failed to get recibo-sueldo');
  }

  return await response.json();
}

export async function getReceiptYears(): Promise<ReceiptYearsResponse> {
  const response = await fetch(`${API_BASE_URL}/receipts/years`, {
    credentials: 'include',
  });

  if (!response.ok) {
    throw await toApiError(response, 'Failed to get receipt years');
  }

  return await response.json();
}

export async function getReceiptMonths(year: number): Promise<ReceiptMonthsResponse> {
  const response = await fetch(`${API_BASE_URL}/receipts/${year}/months`, {
    credentials: 'include',
  });

  if (!response.ok) {
    throw await toApiError(response, `Failed to get receipt months for ${year}`);
  }

  return await response.json();
}

export async function getLatestReceipt(year: number, month: number): Promise<ReceiptSnapshotResponse> {
  const response = await fetch(`${API_BASE_URL}/receipts/${year}/${month}`, {
    credentials: 'include',
  });

  if (!response.ok) {
    throw await toApiError(response, `Failed to get latest receipt for ${year}-${month}`);
  }

  return await response.json();
}

export async function refreshReceipt(year: number, month: number): Promise<ReceiptRefreshResponse> {
  const token = await getCsrfToken();
  const response = await fetch(`${API_BASE_URL}/receipts/${year}/${month}/refresh`, {
    method: 'POST',
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
      'X-CSRF-TOKEN': token,
    },
    body: '{}',
  });

  if (!response.ok) {
    throw await toApiError(response, `Failed to refresh receipt for ${year}-${month}`);
  }

  return await response.json();
}

export async function logReciboSueldoView(
  action: 'page' | 'open',
  reciboId?: string
): Promise<void> {
  const token = await getCsrfToken();

  const response = await fetch(`${API_BASE_URL}/recibo-sueldo/view`, {
    method: 'POST',
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
      'X-CSRF-TOKEN': token,
    },
    body: JSON.stringify({ action, reciboId }),
  });

  if (!response.ok) {
    throw new Error(`Failed to log recibo-sueldo view: ${response.statusText}`);
  }
}

async function getCsrfToken(): Promise<string> {
  const csrfResponse = await fetch(`${API_BASE_URL}/auth/csrf-token`, {
    credentials: 'include',
  });
  if (!csrfResponse.ok) {
    throw new Error('Failed to get CSRF token');
  }
  const { token } = await csrfResponse.json();
  return token;
}

async function toApiError(response: Response, fallbackMessage: string): Promise<Error> {
  if (response.headers.get('Content-Type')?.includes('application/json')) {
    const data = await response.json();
    if (data?.message) {
      return new Error(String(data.message));
    }
  }

  return new Error(`${fallbackMessage}: ${response.statusText}`);
}
