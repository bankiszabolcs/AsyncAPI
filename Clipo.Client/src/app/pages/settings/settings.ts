import { Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgTemplateOutlet } from '@angular/common';
import { toSignal } from '@angular/core/rxjs-interop';
import { TranslocoService, TranslocoPipe } from '@jsverse/transloco';

interface SettingItem {
  label: string;
  description: string;
  icon: string;
  route?: string;
}

interface SettingGroup {
  title: string;
  items: SettingItem[];
}

@Component({
  selector: 'app-settings',
  imports: [RouterLink, NgTemplateOutlet, TranslocoPipe],
  templateUrl: './settings.html',
  styles: ``,
})
export class Settings {
  private readonly transloco = inject(TranslocoService);
  private readonly activeLang = toSignal(this.transloco.langChanges$, {
    initialValue: this.transloco.getActiveLang(),
  });

  readonly groups = computed<SettingGroup[]>(() => {
    void this.activeLang();
    const t = (k: string) => this.transloco.translate(k);
    return [
      {
        title: t('settings.account.title'),
        items: [
          { label: t('settings.account.profile.label'), description: t('settings.account.profile.description'), icon: 'pi-user', route: '/settings/profile' },
          { label: t('settings.account.privacy.label'), description: t('settings.account.privacy.description'), icon: 'pi-lock' },
        ],
      },
      {
        title: t('settings.playback.title'),
        items: [
          { label: t('settings.playback.quality.label'), description: t('settings.playback.quality.description'), icon: 'pi-sliders-h' },
          { label: t('settings.playback.autoplay.label'), description: t('settings.playback.autoplay.description'), icon: 'pi-play' },
        ],
      },
      {
        title: t('settings.notifications.title'),
        items: [
          { label: t('settings.notifications.email.label'), description: t('settings.notifications.email.description'), icon: 'pi-envelope' },
          { label: t('settings.notifications.push.label'), description: t('settings.notifications.push.description'), icon: 'pi-bell' },
        ],
      },
    ];
  });
}
