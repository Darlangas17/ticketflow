import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';

import { page, listItem } from '../../../testing/ticket-fixtures';
import { TicketsApi } from './tickets.api';

describe('TicketsApi', () => {
  let api: TicketsApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(TicketsApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('sends only the filters that are set', async () => {
    const result = firstValueFrom(
      api.list({
        page: 2,
        pageSize: 20,
        status: 'Open',
        search: 'vpn',
        branchId: undefined,
        unassigned: true,
      }),
    );

    const request = http.expectOne((r) => r.url === '/api/tickets');
    expect(request.request.params.keys().sort()).toEqual([
      'page',
      'pageSize',
      'search',
      'status',
      'unassigned',
    ]);
    expect(request.request.params.get('unassigned')).toBe('true');
    request.flush(page([listItem(1)]));

    await expect(result).resolves.toMatchObject({ totalCount: 1 });
  });

  it('uses the documented endpoints for each action', () => {
    void firstValueFrom(api.changeStatus(5, 'Resolved'));
    void firstValueFrom(api.assign(5, null));
    void firstValueFrom(api.addComment(5, 'Done'));
    void firstValueFrom(api.delete(5));

    const status = http.expectOne('/api/tickets/5/status');
    expect([status.request.method, status.request.body]).toEqual(['PATCH', { status: 'Resolved' }]);
    const assign = http.expectOne('/api/tickets/5/assign');
    expect([assign.request.method, assign.request.body]).toEqual(['PATCH', { assigneeId: null }]);
    const comment = http.expectOne('/api/tickets/5/comments');
    expect([comment.request.method, comment.request.body]).toEqual(['POST', { body: 'Done' }]);
    expect(http.expectOne('/api/tickets/5').request.method).toBe('DELETE');
  });
});
