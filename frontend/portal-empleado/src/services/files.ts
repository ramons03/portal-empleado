import { getCsrfToken } from './csrf';
import { readJson, writeJson } from './storage';

export type FileItem = {
  id: string;
  name: string;
  size: number;
  contentType: string;
  uploadedAt: string;
  status: 'uploaded' | 'pending';
  url?: string;
  description?: string;
};

type PresignResponse = {
  uploadUrl: string;
  fileUrl: string;
  key: string;
};

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? '/api';
const FILES_KEY = 'saed.files.items';

export async function listFiles(): Promise<FileItem[]> {
  try {
    const response = await fetch(`${API_BASE_URL}/files`, {
      credentials: 'include',
    });

    if (!response.ok) {
      throw new Error('Failed to load files');
    }

    const data = await response.json();
    if (!Array.isArray(data)) return [];
    return data as FileItem[];
  } catch {
    return readJson<FileItem[]>(FILES_KEY, []);
  }
}

export async function uploadFile(file: File, description?: string): Promise<FileItem> {
  const payload = {
    fileName: file.name,
    contentType: file.type || 'application/octet-stream',
    size: file.size,
    description: description?.trim() ?? '',
  };

  try {
    const token = await getCsrfToken();
    const presignResponse = await fetch(`${API_BASE_URL}/files/presign`, {
      method: 'POST',
      credentials: 'include',
      headers: {
        'Content-Type': 'application/json',
        'X-CSRF-TOKEN': token,
      },
      body: JSON.stringify(payload),
    });

    if (!presignResponse.ok) {
      throw new Error('Failed to get upload url');
    }

    const presign = await presignResponse.json() as PresignResponse;

    const uploadResponse = await fetch(presign.uploadUrl, {
      method: 'PUT',
      headers: {
        'Content-Type': payload.contentType,
      },
      body: file,
    });

    if (!uploadResponse.ok) {
      throw new Error('Failed to upload file to S3');
    }

    const item: FileItem = {
      id: presign.key,
      name: file.name,
      size: file.size,
      contentType: payload.contentType,
      uploadedAt: new Date().toISOString(),
      status: 'uploaded',
      url: presign.fileUrl,
      description: payload.description || undefined,
    };

    return item;
  } catch {
    const item: FileItem = {
      id: `local-${crypto.randomUUID()}`,
      name: file.name,
      size: file.size,
      contentType: payload.contentType,
      uploadedAt: new Date().toISOString(),
      status: 'pending',
      description: payload.description || undefined,
    };

    const existing = readJson<FileItem[]>(FILES_KEY, []);
    writeJson(FILES_KEY, [item, ...existing]);
    return item;
  }
}

export async function deleteFile(fileId: string): Promise<void> {
  try {
    const token = await getCsrfToken();
    const response = await fetch(`${API_BASE_URL}/files/${encodeURIComponent(fileId)}`, {
      method: 'DELETE',
      credentials: 'include',
      headers: {
        'X-CSRF-TOKEN': token,
      },
    });

    if (!response.ok) {
      throw new Error('Failed to delete file');
    }
  } catch {
    const existing = readJson<FileItem[]>(FILES_KEY, []);
    writeJson(FILES_KEY, existing.filter((item) => item.id !== fileId));
  }
}
