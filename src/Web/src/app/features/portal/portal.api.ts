import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { toProblem } from '../../core/api/problem-details';
import { ProjectStatus } from '../../core/projects/projects.api';

/** The client portal's wire shapes (`src/Api/Features/Portal`). Money and percentages arrive as two-decimal strings. */
export interface PortalProject {
  readonly id: string;
  readonly name: string;
  readonly status: ProjectStatus;
  readonly contractType: string;
  readonly paid: string;
  readonly remaining?: string;
  readonly pendingCount: number;
}

export interface PortalSection {
  readonly nameAr: string;
  readonly nameEn: string;
  readonly completion: string;
}

export interface PortalHome extends Omit<PortalProject, 'pendingCount'> {
  readonly plannedStart?: string;
  readonly plannedEnd?: string;
  readonly isStalled: boolean;
  readonly completion?: string;
  readonly sections: readonly PortalSection[];
  readonly contractValue?: string;
  readonly paid: string;
  readonly remaining?: string;
  readonly pendingApprovals: number;
}

export interface PortalExtract {
  readonly id: string;
  readonly number: number;
  readonly periodStart: string;
  readonly periodEnd: string;
  readonly certifiedWork: string;
  readonly netPayable: string;
  readonly paid: string;
  readonly isPaid: boolean;
}

export interface PortalPhoto {
  readonly id: string;
  readonly caption: string | null;
  readonly takenAt: string;
}

export interface PortalChangeOrderLine {
  readonly lineNumber: number;
  readonly description: string;
  readonly unit: string;
  readonly quantity: number;
  readonly unitPrice: string;
  readonly total: string;
}

export interface PortalChangeOrder {
  readonly id: string;
  readonly number: number;
  readonly title: string;
  readonly status: string;
  readonly total: string;
  readonly canDecide: boolean;
  readonly lines: readonly PortalChangeOrderLine[];
}

export interface PortalChangeOrderDetail extends PortalChangeOrder {
  readonly submittedAt: string | null;
  readonly contractValueBefore?: string;
  readonly contractValueAfter?: string;
}

/** Why a portal read was refused: no session, a temporary password still in force, or anything else. */
export type PortalRefusal = 'sign-in' | 'password' | 'other';

@Injectable({ providedIn: 'root' })
export class PortalApi {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  async signIn(userName: string, password: string): Promise<void> {
    await firstValueFrom(this.http.post<void>('api/portal/auth/sign-in', { userName, password }));
  }

  async changePassword(currentPassword: string, newPassword: string): Promise<void> {
    await firstValueFrom(this.http.post<void>('api/portal/auth/change-password', { currentPassword, newPassword }));
  }

  /** Sign-out is the one door both audiences share; it only clears the cookie. */
  async signOut(): Promise<void> {
    await firstValueFrom(this.http.post<void>('api/auth/sign-out', {}));
  }

  projects(): Promise<readonly PortalProject[]> {
    return firstValueFrom(this.http.get<PortalProject[]>('api/portal/projects'));
  }

  home(projectId: string): Promise<PortalHome> {
    return firstValueFrom(this.http.get<PortalHome>(`api/portal/projects/${projectId}`));
  }

  extracts(projectId: string): Promise<readonly PortalExtract[]> {
    return firstValueFrom(this.http.get<PortalExtract[]>(`api/portal/projects/${projectId}/extracts`));
  }

  photos(projectId: string): Promise<readonly PortalPhoto[]> {
    return firstValueFrom(this.http.get<PortalPhoto[]>(`api/portal/projects/${projectId}/photos`));
  }

  photo(projectId: string, photoId: string): Promise<Blob> {
    return firstValueFrom(this.http.get(`api/portal/projects/${projectId}/photos/${photoId}`, { responseType: 'blob' }));
  }

  changeOrders(projectId: string): Promise<readonly PortalChangeOrder[]> {
    return firstValueFrom(this.http.get<PortalChangeOrder[]>(`api/portal/projects/${projectId}/change-orders`));
  }

  changeOrder(projectId: string, orderId: string): Promise<PortalChangeOrderDetail> {
    return firstValueFrom(this.http.get<PortalChangeOrderDetail>(`api/portal/projects/${projectId}/change-orders/${orderId}`));
  }

  approve(projectId: string, orderId: string): Promise<PortalChangeOrder> {
    return firstValueFrom(this.http.post<PortalChangeOrder>(`api/portal/projects/${projectId}/change-orders/${orderId}/approve`, {}));
  }

  reject(projectId: string, orderId: string, reason: string): Promise<PortalChangeOrder> {
    return firstValueFrom(this.http.post<PortalChangeOrder>(`api/portal/projects/${projectId}/change-orders/${orderId}/reject`, { reason }));
  }

  /**
   * Classifies a failed read. The server decides; this only picks the screen to show. A company-level read (the project
   * list) answers the blanket 403 to a caller with no session, so there a 403 means signed out; on a project read it
   * means not assigned and stays a failure.
   */
  refusal(error: unknown, forbiddenMeansSignedOut = false): PortalRefusal {
    const problem = toProblem(error);
    if (problem.code === 'auth.password_change_required') {
      return 'password';
    }
    return problem.status === 401 || (forbiddenMeansSignedOut && problem.status === 403) ? 'sign-in' : 'other';
  }

  /** Sends a refused read to the screen that fixes it. Returns true when it navigated. */
  routeRefusal(error: unknown, forbiddenMeansSignedOut = false): boolean {
    const kind = this.refusal(error, forbiddenMeansSignedOut);
    if (kind === 'other') {
      return false;
    }
    void this.router.navigateByUrl(kind === 'password' ? '/portal/change-password' : '/portal/sign-in', { replaceUrl: true });
    return true;
  }
}
