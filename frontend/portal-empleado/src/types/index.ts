export interface User {
  id: string;
  email: string;
  fullName: string;
  pictureUrl?: string;
  createdAt: string;
}

export interface ApiError {
  message: string;
  status: number;
}
