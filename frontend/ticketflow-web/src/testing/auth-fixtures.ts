import { AuthResponse, User, UserRole } from '../app/core/models/auth.models';
import { SESSION_STORAGE_KEY, StoredSession } from '../app/core/auth/auth.service';

// Test data shared by the specs. All values are fictional.

export const USERS: Record<UserRole, User> = {
  Admin: { id: 1, fullName: 'Laura Mejia', email: 'admin@ticketflow.demo', role: 'Admin' },
  Technician: {
    id: 2,
    fullName: 'Carlos Rivera',
    email: 'carlos.rivera@ticketflow.demo',
    role: 'Technician',
  },
  Requester: {
    id: 5,
    fullName: 'Maria Lopez',
    email: 'maria.lopez@ticketflow.demo',
    role: 'Requester',
  },
};

export function authResponse(
  role: UserRole = 'Requester',
  expiresInMs = 60 * 60 * 1000,
): AuthResponse {
  return {
    accessToken: `token-for-${role.toLowerCase()}`,
    expiresAt: new Date(Date.now() + expiresInMs).toISOString(),
    user: USERS[role],
  };
}

export function saveSession(session: StoredSession): void {
  localStorage.setItem(SESSION_STORAGE_KEY, JSON.stringify(session));
}

export function storedSession(): StoredSession | null {
  const raw = localStorage.getItem(SESSION_STORAGE_KEY);
  return raw ? (JSON.parse(raw) as StoredSession) : null;
}
