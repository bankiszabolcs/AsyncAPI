import { Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { toSignal } from '@angular/core/rxjs-interop';
import { Dialog } from 'primeng/dialog';
import { InputText } from 'primeng/inputtext';
import { Select } from 'primeng/select';
import { TranslocoService, TranslocoPipe } from '@jsverse/transloco';
import { VISIBILITY_OPTIONS } from '../../core/models/visibility.model';

export interface PlaylistFormData {
  title: string;
  description: string | null;
  visibilityId: number;
}

@Component({
  selector: 'app-playlist-form-dialog',
  imports: [FormsModule, Dialog, InputText, Select, TranslocoPipe],
  templateUrl: './playlist-form-dialog.html',
})
export class PlaylistFormDialog {
  readonly visible      = input.required<boolean>();
  readonly mode         = input<'create' | 'edit'>('create');
  readonly initialTitle = input('');
  readonly initialDesc  = input('');
  readonly initialVis   = input(3);
  readonly isSaving     = input(false);

  readonly visibleChange = output<boolean>();
  readonly saved         = output<PlaylistFormData>();

  readonly formTitle = signal('');
  readonly formDesc  = signal('');
  readonly formVis   = signal(3);

  private readonly transloco = inject(TranslocoService);
  private readonly activeLang = toSignal(this.transloco.langChanges$, {
    initialValue: this.transloco.getActiveLang(),
  });

  readonly visibilityOptions = computed(() => {
    void this.activeLang();
    return VISIBILITY_OPTIONS.map(o => ({
      value: o.value,
      icon: o.icon,
      label: this.transloco.translate(`visibility.${o.translationKey}.label`),
    }));
  });

  readonly selectPt = {
    label: { class: 'py-2' },
  };

  constructor() {
    effect(() => {
      if (this.visible()) {
        this.formTitle.set(this.initialTitle());
        this.formDesc.set(this.initialDesc());
        this.formVis.set(this.initialVis());
      }
    });
  }

  close(): void {
    this.visibleChange.emit(false);
  }

  submit(): void {
    const title = this.formTitle().trim();
    if (!title || this.isSaving()) return;
    this.saved.emit({
      title,
      description: this.formDesc().trim() || null,
      visibilityId: this.formVis(),
    });
  }
}
