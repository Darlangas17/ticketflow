import { navigationFor } from './navigation';

describe('navigationFor', () => {
  const labels = (role: Parameters<typeof navigationFor>[0]) =>
    navigationFor(role).flatMap((section) => section.items.map((item) => item.label));

  it('shows administration only to admins', () => {
    expect(labels('Admin')).toEqual(['Dashboard', 'Tickets', 'Users', 'Branches', 'Categories']);
    expect(labels('Technician')).toEqual(['Dashboard', 'Tickets']);
  });

  it('calls the ticket list "My tickets" for requesters', () => {
    expect(labels('Requester')).toEqual(['Dashboard', 'My tickets']);
  });

  it('drops sections that end up empty', () => {
    expect(navigationFor('Technician').map((section) => section.title)).toEqual([undefined]);
  });
});
