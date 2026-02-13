import type { User } from '../types';

const API_BASE_URL = 'https://localhost:7079/api';

/**
 * Get current user information
 */
export async function getCurrentUser(): Promise<User | null> {
  try {
    const response = await fetch(`${API_BASE_URL}/auth/me`, {
      credentials: 'include', // Include cookies for authentication
      headers: {
        'Content-Type': 'application/json',
      },
    });

    if (response.status === 401) {
      return null; // User is not authenticated
    }

    if (!response.ok) {
      throw new Error(`Failed to get current user: ${response.statusText}`);
    }

    return await response.json();
  } catch (error) {
    console.error('Error fetching current user:', error);
    throw error;
  }
}

/**
 * Logout the current user
 */
export async function logout(): Promise<void> {
  try {
    // First, get CSRF token
    const csrfResponse = await fetch(`${API_BASE_URL}/auth/csrf-token`, {
      credentials: 'include',
    });
    
    if (!csrfResponse.ok) {
      throw new Error('Failed to get CSRF token');
    }
    
    const { token } = await csrfResponse.json();

    // Then, perform logout with CSRF token
    const response = await fetch(`${API_BASE_URL}/auth/logout`, {
      method: 'POST',
      credentials: 'include',
      headers: {
        'Content-Type': 'application/json',
        'X-CSRF-TOKEN': token,
      },
    });

    if (!response.ok) {
      throw new Error(`Logout failed: ${response.statusText}`);
    }
  } catch (error) {
    console.error('Error logging out:', error);
    throw error;
  }
}

/**
 * Get the login URL
 */
export function getLoginUrl(): string {
  return `${API_BASE_URL}/auth/login`;
}
