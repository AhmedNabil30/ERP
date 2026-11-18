import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { ContractType, ProjectStatus } from '../../../core/projects/projects.api';

/**
 * `POST /api/projects` — `CreateProject.Request`, field for field.
 *
 * **There is no contract value, and there must not be one.** The server derives it: Lump Sum takes the
 * signed BOQ's total (`POST /api/projects/{id}/sign`), Design is area x rate, Cost Plus has none.
 * `holdPercentage` and `clientAdvance` are wire fractions (`"0.20"` is 20%, D-151). Both are
 * Lump Sum only and are omitted (`null`) to take the spec defaults. `status` is never sent: a new project
 * starts in Setup.
 */
export interface ProjectCreate {
  readonly name: string;
  readonly clientId: string;
  readonly contractType: ContractType;
}

/** `CreateProject.Response`, narrowed to what the screen uses. */
export interface ProjectCreated {
  readonly id: string;
  readonly code: string;
  readonly name: string;
  readonly status: ProjectStatus;
}

@Injectable({ providedIn: 'root' })
export class ProjectCreateApi {
  private readonly http = inject(HttpClient);

  async create(project: ProjectCreate): Promise<ProjectCreated> {
    return await firstValueFrom(this.http.post<ProjectCreated>('api/projects', project));
  }
}
