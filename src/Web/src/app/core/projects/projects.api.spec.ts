import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { ProjectsApi, TeamRead } from './projects.api';

describe('ProjectsApi team calls', () => {
  let api: ProjectsApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(ProjectsApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('reads the team with the caller permission and each member rule', async () => {
    const body: TeamRead = {
      canManage: true,
      members: [
        { assignmentId: 'a', userId: 'u', fullName: 'x', role: 'Finance', level: 'Standard', origin: 'Automatic', canRemove: false, assignedAt: 't' },
      ],
    };
    const read = api.teamRead('p-1');
    http.expectOne('api/projects/p-1/team').flush(body);

    expect(await read).toEqual(body);
  });

  it('keeps team() as the members of the read', async () => {
    const read = api.team('p-1');
    http.expectOne('api/projects/p-1/team').flush({ canManage: false, members: [] });

    expect(await read).toEqual([]);
  });

  it('reads the candidates, assigns and removes on the assignment routes', async () => {
    const candidates = api.teamCandidates('p-1');
    http.expectOne({ method: 'GET', url: 'api/projects/p-1/assignments/candidates' }).flush([]);
    expect(await candidates).toEqual([]);

    const assigned = api.assign('p-1', { userId: 'u-1', level: 'Junior' });
    const post = http.expectOne({ method: 'POST', url: 'api/projects/p-1/assignments' });
    expect(post.request.body).toEqual({ userId: 'u-1', level: 'Junior' });
    post.flush({});
    await assigned;

    const removed = api.removeMember('p-1', 'a-9');
    http.expectOne({ method: 'POST', url: 'api/projects/p-1/assignments/a-9/revoke' }).flush(null, { status: 204, statusText: 'No Content' });
    await removed;
  });
});
