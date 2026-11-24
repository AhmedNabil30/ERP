import { ChangeDetectionStrategy, Component, DestroyRef, effect, inject, input, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';

import { I18nService } from '../../core/i18n/i18n.service';
import { KaffButton } from '../../shared/kaff-button/kaff-button';
import { KaffState } from '../../shared/kaff-state/kaff-state';
import { PortalApi, PortalPhoto } from './portal.api';
import { portalDate } from './portal-format';

/**
 * P8. The photos Kaff chose to publish, newest first. A photo is bytes behind a session cookie, so each is fetched as a
 * blob and shown from an object URL (revoked on destroy). Tapping one opens it large in place.
 */
@Component({
  selector: 'kaff-portal-photos-page',
  imports: [KaffButton, KaffState, RouterLink],
  styleUrl: './portal.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="wrap">
      @if (opened(); as photo) {
        <button type="button" class="back" style="border: 0; background: none; font: inherit; cursor: pointer" data-testid="portal-photo-close" (click)="opened.set(null)">
          <svg class="i s flip" viewBox="0 0 24 24" aria-hidden="true"><path d="M19 12H5M11 6l-6 6 6 6" /></svg>
          {{ i18n.t('portal.photos.title') }}
        </button>
        <figure class="ph open" style="margin: 0" data-testid="portal-photo-open">
          <div class="pic">
            @if (thumbs()[photo.id]; as src) {
              <img [src]="src" [alt]="photo.caption ?? ''" />
            }
          </div>
          <figcaption class="cap">
            @if (photo.caption) {
              <b dir="auto">{{ photo.caption }}</b>
            }
            <small class="fig">{{ date(photo.takenAt) }}</small>
          </figcaption>
        </figure>
      } @else {
        <a class="back" [routerLink]="['/portal/projects', projectId()]" data-testid="portal-back">
          <svg class="i s flip" viewBox="0 0 24 24" aria-hidden="true"><path d="M19 12H5M11 6l-6 6 6 6" /></svg>
          {{ i18n.t('portal.back') }}
        </a>
        <h1>{{ i18n.t('portal.photos.title') }}</h1>

        @if (loading()) {
          <kaff-state mode="loading" titleKey="portal.loading" />
        } @else if (failed()) {
          <kaff-state mode="refusal" titleKey="portal.load_failed" testId="portal-failed">
            <kaff-button stateAction variant="secondary" testId="portal-retry" (clicked)="load(projectId())">{{ i18n.t('portal.retry') }}</kaff-button>
          </kaff-state>
        } @else if (photos().length === 0) {
          <kaff-state mode="empty" icon="cam" titleKey="portal.photos.empty" />
        } @else {
          <ul class="grid" data-testid="portal-photos">
            @for (photo of photos(); track photo.id) {
              <li>
                <button type="button" class="ph" data-testid="portal-photo" (click)="opened.set(photo)">
                  <span class="pic">
                    @if (thumbs()[photo.id]; as src) {
                      <img [src]="src" [alt]="photo.caption ?? ''" />
                    }
                  </span>
                  <span class="cap">
                    @if (photo.caption) {
                      <b dir="auto" style="font-size: 0.9375rem">{{ photo.caption }}</b>
                    }
                    <small class="fig">{{ date(photo.takenAt) }}</small>
                  </span>
                </button>
              </li>
            }
          </ul>
        }
      }
    </div>
  `,
})
export class PortalPhotosPage {
  readonly projectId = input.required<string>();

  private readonly api = inject(PortalApi);
  protected readonly i18n = inject(I18nService);

  protected readonly photos = signal<readonly PortalPhoto[]>([]);
  protected readonly opened = signal<PortalPhoto | null>(null);
  protected readonly thumbs = signal<Readonly<Record<string, string>>>({});
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);

  constructor() {
    effect(() => {
      const id = this.projectId();
      untracked(() => void this.load(id));
    });
    inject(DestroyRef).onDestroy(() => Object.values(this.thumbs()).forEach((url) => URL.revokeObjectURL(url)));
  }

  protected date(value: string): string {
    return portalDate(this.i18n, value);
  }

  protected async load(projectId: string): Promise<void> {
    this.loading.set(true);
    this.failed.set(false);
    try {
      const list = await this.api.photos(projectId);
      this.photos.set([...list].sort((a, b) => b.takenAt.localeCompare(a.takenAt)));
      list.forEach((photo) => void this.loadPicture(projectId, photo.id));
    } catch (error) {
      if (this.api.routeRefusal(error)) {
        return;
      }
      this.failed.set(true);
    } finally {
      this.loading.set(false);
    }
  }

  private async loadPicture(projectId: string, photoId: string): Promise<void> {
    try {
      const url = URL.createObjectURL(await this.api.photo(projectId, photoId));
      this.thumbs.update((current) => ({ ...current, [photoId]: url }));
    } catch {
      // A picture that will not load leaves its tile empty; the caption and date still read.
    }
  }
}
