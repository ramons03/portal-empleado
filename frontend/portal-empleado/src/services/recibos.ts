export type ReciboItem = {
  id: string;
  periodo: string;
  importe: number;
  moneda: string;
  estado: string;
  fechaEmision: string;
  pdfUrl?: string | null;
};

export type RecibosResponse = {
  cuil: string;
  items: ReciboItem[];
};

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? '/api';

export function getReciboPdfUrl(reciboId: string): string {
  return `${API_BASE_URL}/recibos/${encodeURIComponent(reciboId)}/pdf`;
}

export async function getRecibos(): Promise<RecibosResponse> {
  const response = await fetch(`${API_BASE_URL}/recibos`, {
    credentials: 'include',
  });

  if (!response.ok) {
    if (response.headers.get('Content-Type')?.includes('application/json')) {
      const data = await response.json();
      if (data?.message) {
        throw new Error(String(data.message));
      }
    }
    throw new Error(`Failed to get recibos: ${response.statusText}`);
  }

  return await response.json();
}

export async function logRecibosView(
  action: 'page' | 'open',
  reciboId?: string
): Promise<void> {
  const csrfResponse = await fetch(`${API_BASE_URL}/auth/csrf-token`, {
    credentials: 'include',
  });
  if (!csrfResponse.ok) {
    throw new Error('Failed to get CSRF token');
  }
  const { token } = await csrfResponse.json();

  const response = await fetch(`${API_BASE_URL}/recibos/view`, {
    method: 'POST',
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
      'X-CSRF-TOKEN': token,
    },
    body: JSON.stringify({ action, reciboId }),
  });

  if (!response.ok) {
    throw new Error(`Failed to log recibos view: ${response.statusText}`);
  }
}
