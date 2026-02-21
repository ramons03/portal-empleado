const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? '/api';

export async function getCsrfToken(): Promise<string> {
  const response = await fetch(`${API_BASE_URL}/auth/csrf-token`, {
    credentials: 'include',
  });

  if (!response.ok) {
    throw new Error('Failed to get CSRF token');
  }

  const data = await response.json();
  return String(data.token ?? '');
}
