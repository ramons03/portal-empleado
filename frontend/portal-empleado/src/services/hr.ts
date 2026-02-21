import { getCsrfToken } from './csrf';
import { readJson, writeJson } from './storage';

export type SelfServiceProfile = {
  phone: string;
  address: string;
  emergencyName: string;
  emergencyPhone: string;
};

export type CertificateRequest = {
  type: 'laboral' | 'sueldo' | 'antiguedad';
  comment: string;
};

export type CbuUpdate = {
  cbu: string;
  alias: string;
};

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? '/api';
const PROFILE_KEY = 'saed.selfservice.profile';

const emptyProfile: SelfServiceProfile = {
  phone: '',
  address: '',
  emergencyName: '',
  emergencyPhone: '',
};

export async function getSelfServiceProfile(): Promise<SelfServiceProfile> {
  try {
    const response = await fetch(`${API_BASE_URL}/self-service/profile`, {
      credentials: 'include',
    });

    if (!response.ok) {
      throw new Error('Failed to load self service profile');
    }

    const data = await response.json();
    return {
      phone: String(data?.phone ?? ''),
      address: String(data?.address ?? ''),
      emergencyName: String(data?.emergencyName ?? ''),
      emergencyPhone: String(data?.emergencyPhone ?? ''),
    };
  } catch {
    return readJson<SelfServiceProfile>(PROFILE_KEY, emptyProfile);
  }
}

export async function saveSelfServiceProfile(profile: SelfServiceProfile): Promise<void> {
  try {
    const token = await getCsrfToken();
    const response = await fetch(`${API_BASE_URL}/self-service/profile`, {
      method: 'POST',
      credentials: 'include',
      headers: {
        'Content-Type': 'application/json',
        'X-CSRF-TOKEN': token,
      },
      body: JSON.stringify(profile),
    });

    if (!response.ok) {
      throw new Error('Failed to save self service profile');
    }
  } catch {
    writeJson(PROFILE_KEY, profile);
  }
}

export async function submitCertificateRequest(
  request: CertificateRequest
): Promise<void> {
  const payload = {
    type: request.type,
    comment: request.comment.trim(),
  };

  try {
    const token = await getCsrfToken();
    const response = await fetch(`${API_BASE_URL}/self-service/certificates`, {
      method: 'POST',
      credentials: 'include',
      headers: {
        'Content-Type': 'application/json',
        'X-CSRF-TOKEN': token,
      },
      body: JSON.stringify(payload),
    });

    if (!response.ok) {
      throw new Error('Failed to submit certificate request');
    }
  } catch {
    const history = readJson<CertificateRequest[]>(
      'saed.selfservice.certificates',
      []
    );
    writeJson('saed.selfservice.certificates', [payload, ...history]);
  }
}

export async function submitCbuUpdate(update: CbuUpdate): Promise<void> {
  const payload = {
    cbu: update.cbu.trim(),
    alias: update.alias.trim(),
  };

  try {
    const token = await getCsrfToken();
    const response = await fetch(`${API_BASE_URL}/self-service/cbu`, {
      method: 'POST',
      credentials: 'include',
      headers: {
        'Content-Type': 'application/json',
        'X-CSRF-TOKEN': token,
      },
      body: JSON.stringify(payload),
    });

    if (!response.ok) {
      throw new Error('Failed to submit CBU update');
    }
  } catch {
    writeJson('saed.selfservice.cbu', payload);
  }
}
