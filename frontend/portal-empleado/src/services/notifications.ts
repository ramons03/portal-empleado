import { getCsrfToken } from './csrf';
import { readJson, writeJson } from './storage';

export type NotificationAudience = 'all' | 'colegio' | 'area';

export type NotificationChannels = {
  inApp: boolean;
  email: boolean;
  push: boolean;
  sms: boolean;
};

export type NotificationPreference = NotificationChannels;

export type NotificationDraft = {
  title: string;
  message: string;
  audience: NotificationAudience;
  channels: NotificationChannels;
};

export type NotificationItem = {
  id: string;
  title: string;
  message: string;
  audience: NotificationAudience;
  channels: NotificationChannels;
  createdAt: string;
};

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? '/api';
const PREFS_KEY = 'saed.notifications.prefs';
const SENT_KEY = 'saed.notifications.sent';

const defaultPrefs: NotificationPreference = {
  inApp: true,
  email: false,
  push: false,
  sms: false,
};

export async function getNotificationPreferences(): Promise<NotificationPreference> {
  try {
    const response = await fetch(`${API_BASE_URL}/notifications/preferences`, {
      credentials: 'include',
    });

    if (!response.ok) {
      throw new Error('Failed to load notification preferences');
    }

    const data = await response.json();
    return {
      inApp: Boolean(data?.inApp ?? defaultPrefs.inApp),
      email: Boolean(data?.email ?? defaultPrefs.email),
      push: Boolean(data?.push ?? defaultPrefs.push),
      sms: Boolean(data?.sms ?? defaultPrefs.sms),
    };
  } catch {
    return readJson<NotificationPreference>(PREFS_KEY, defaultPrefs);
  }
}

export async function saveNotificationPreferences(
  prefs: NotificationPreference
): Promise<void> {
  try {
    const token = await getCsrfToken();
    const response = await fetch(`${API_BASE_URL}/notifications/preferences`, {
      method: 'POST',
      credentials: 'include',
      headers: {
        'Content-Type': 'application/json',
        'X-CSRF-TOKEN': token,
      },
      body: JSON.stringify(prefs),
    });

    if (!response.ok) {
      throw new Error('Failed to save notification preferences');
    }
  } catch {
    writeJson(PREFS_KEY, prefs);
  }
}

export async function getSentNotifications(): Promise<NotificationItem[]> {
  try {
    const response = await fetch(`${API_BASE_URL}/notifications/sent`, {
      credentials: 'include',
    });

    if (!response.ok) {
      throw new Error('Failed to load notifications');
    }

    const data = await response.json();
    if (!Array.isArray(data)) {
      return [];
    }

    return data as NotificationItem[];
  } catch {
    return readJson<NotificationItem[]>(SENT_KEY, []);
  }
}

export async function sendNotification(
  draft: NotificationDraft
): Promise<NotificationItem> {
  const payload = {
    title: draft.title.trim(),
    message: draft.message.trim(),
    audience: draft.audience,
    channels: draft.channels,
  };

  try {
    const token = await getCsrfToken();
    const response = await fetch(`${API_BASE_URL}/notifications/send`, {
      method: 'POST',
      credentials: 'include',
      headers: {
        'Content-Type': 'application/json',
        'X-CSRF-TOKEN': token,
      },
      body: JSON.stringify(payload),
    });

    if (!response.ok) {
      throw new Error('Failed to send notification');
    }

    return await response.json() as NotificationItem;
  } catch {
    const fallback: NotificationItem = {
      id: `local-${crypto.randomUUID()}`,
      title: payload.title,
      message: payload.message,
      audience: payload.audience,
      channels: payload.channels,
      createdAt: new Date().toISOString(),
    };

    const existing = readJson<NotificationItem[]>(SENT_KEY, []);
    writeJson(SENT_KEY, [fallback, ...existing]);
    return fallback;
  }
}
