// Mirrors the API contracts in TicketFlow.Application/Auth/AuthDtos.cs.

export type UserRole = 'Requester' | 'Technician' | 'Admin';

export const USER_ROLES: readonly UserRole[] = ['Requester', 'Technician', 'Admin'];

export interface User {
  id: number;
  fullName: string;
  email: string;
  role: UserRole;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  fullName: string;
  email: string;
  password: string;
}

export interface AuthResponse {
  accessToken: string;
  /** ISO-8601 UTC timestamp. */
  expiresAt: string;
  user: User;
}
