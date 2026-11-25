import { UserRole } from '../models/auth.models';

export interface NavItem {
  label: string;
  /** Material icon name. */
  icon: string;
  path: string;
  /** Roles that see the item; omitted means every signed-in user. */
  roles?: readonly UserRole[];
  /** Label for requesters, whose list only shows their own tickets. */
  requesterLabel?: string;
}

export interface NavSection {
  title?: string;
  items: readonly NavItem[];
}

export const NAVIGATION: readonly NavSection[] = [
  {
    items: [
      { label: 'Dashboard', icon: 'dashboard', path: '/dashboard' },
      {
        label: 'Tickets',
        icon: 'confirmation_number',
        path: '/tickets',
        requesterLabel: 'My tickets',
      },
    ],
  },
  {
    title: 'Administration',
    items: [
      { label: 'Users', icon: 'group', path: '/admin/users', roles: ['Admin'] },
      { label: 'Branches', icon: 'store', path: '/admin/branches', roles: ['Admin'] },
      { label: 'Categories', icon: 'category', path: '/admin/categories', roles: ['Admin'] },
    ],
  },
];

/** The sections and items a role can see; sections left empty are dropped. */
export function navigationFor(role: UserRole): NavSection[] {
  return NAVIGATION.map((section) => ({
    ...section,
    items: section.items
      .filter((item) => !item.roles || item.roles.includes(role))
      .map((item) =>
        role === 'Requester' && item.requesterLabel
          ? { ...item, label: item.requesterLabel }
          : item,
      ),
  })).filter((section) => section.items.length > 0);
}
