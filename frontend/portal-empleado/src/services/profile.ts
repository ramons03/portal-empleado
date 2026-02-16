const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? '/api';

export async function updateCuil(cuil: string): Promise<string> {
  const csrfResponse = await fetch(`${API_BASE_URL}/auth/csrf-token`, {
    credentials: 'include',
  });

  if (!csrfResponse.ok) {
    throw new Error('Failed to get CSRF token');
  }

  const { token } = await csrfResponse.json();

  const response = await fetch(`${API_BASE_URL}/profile/cuil`, {
    method: 'POST',
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
      'X-CSRF-TOKEN': token,
    },
    body: JSON.stringify({ cuil }),
  });

  if (!response.ok) {
    if (response.headers.get('Content-Type')?.includes('application/json')) {
      const data = await response.json();
      if (data?.message) {
        throw new Error(String(data.message));
      }
    }
    throw new Error(`Failed to update CUIL: ${response.statusText}`);
  }

  const data = await response.json();
  return String(data.cuil ?? cuil);
}
