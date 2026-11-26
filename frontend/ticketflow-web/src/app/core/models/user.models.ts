import { UserRole } from './auth.models';

// Mirrors TicketFlow.Application/Users/UserDtos.cs.

export interface UserAccount {
  id: number;
  fullName: string;
  email: string;
  role: UserRole;
  isActive: boolean;
  createdAt: string;
}

export interface UserListQuery {
  page: number;
  pageSize: number;
  role?: UserRole;
  isActive?: boolean;
  search?: string;
}

export interface CreateUserRequest {
  fullName: string;
  email: string;
  password: string;
  role: UserRole;
}

export interface UpdateUserRequest {
  fullName: string;
  email: string;
  role: UserRole;
  isActive: boolean;
  /** Only sent to set a new password. */
  password?: string;
}
