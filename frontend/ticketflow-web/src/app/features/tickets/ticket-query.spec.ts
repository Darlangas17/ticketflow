import { convertToParamMap } from '@angular/router';

import { DEFAULT_PAGE_SIZE, hasFilters, parseTicketQuery, toQueryParams } from './ticket-query';

describe('ticket query ↔ URL', () => {
  const parse = (params: Record<string, string>) => parseTicketQuery(convertToParamMap(params));

  it('uses defaults for an empty URL', () => {
    expect(parse({})).toEqual({
      page: 1,
      pageSize: DEFAULT_PAGE_SIZE,
      status: undefined,
      priority: undefined,
      branchId: undefined,
      categoryId: undefined,
      assigneeId: undefined,
      unassigned: undefined,
      search: undefined,
      sort: '-createdAt',
    });
  });

  it('reads valid filters', () => {
    expect(
      parse({
        page: '3',
        pageSize: '50',
        status: 'OnHold',
        priority: 'Critical',
        branchId: '2',
        categoryId: '4',
        assigneeId: '9',
        search: '  vpn  ',
        sort: '-priority',
      }),
    ).toMatchObject({
      page: 3,
      pageSize: 50,
      status: 'OnHold',
      priority: 'Critical',
      branchId: 2,
      categoryId: 4,
      assigneeId: 9,
      search: 'vpn',
      sort: '-priority',
    });
  });

  it('ignores invalid values instead of failing', () => {
    expect(
      parse({
        page: '-2',
        pageSize: '1000',
        status: 'Sleeping',
        priority: 'urgent',
        branchId: 'abc',
        sort: 'password',
      }),
    ).toMatchObject({
      page: 1,
      pageSize: DEFAULT_PAGE_SIZE,
      status: undefined,
      priority: undefined,
      branchId: undefined,
      sort: '-createdAt',
    });
  });

  it('gives "unassigned" priority over an assignee id (the API rejects both)', () => {
    expect(parse({ unassigned: 'true', assigneeId: '3' })).toMatchObject({
      unassigned: true,
      assigneeId: undefined,
    });
  });

  it('caps the search text at 100 characters', () => {
    expect(parse({ search: 'x'.repeat(150) }).search).toHaveLength(100);
  });

  it('leaves defaults out of the URL', () => {
    expect(toQueryParams(parse({}))).toEqual({
      page: null,
      pageSize: null,
      status: null,
      priority: null,
      branchId: null,
      categoryId: null,
      assigneeId: null,
      unassigned: null,
      search: null,
      sort: null,
    });
  });

  it('round-trips a full query', () => {
    const params = {
      page: '2',
      status: 'Open',
      unassigned: 'true',
      search: 'printer',
      sort: 'title',
    };
    const query = parse(params);

    expect(parse(stringify(toQueryParams(query)))).toEqual(query);
  });

  it('detects filters but not paging or sorting', () => {
    expect(hasFilters(parse({ page: '4', sort: 'title' }))).toBe(false);
    expect(hasFilters(parse({ unassigned: 'true' }))).toBe(true);
    expect(hasFilters(parse({ search: 'vpn' }))).toBe(true);
  });
});

function stringify(params: Record<string, unknown>): Record<string, string> {
  return Object.fromEntries(
    Object.entries(params)
      .filter(([, value]) => value !== null && value !== undefined)
      .map(([key, value]) => [key, String(value)]),
  );
}
