import { Component, effect, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Dialog } from 'primeng/dialog';
import { InputText } from 'primeng/inputtext';
import { Select } from 'primeng/select';
import { VISIBILITY_OPTIONS } from '../../core/models/visibility.model';

export interface PlaylistFormData {
  title: string;
  description: string | null;
  visibilityId: number;
}

@Component({
  selector: 'app-playlist-form-dialog',
  imports: [FormsModule, Dialog, InputText, Select],
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

  readonly visibilityOptions = VISIBILITY_OPTIONS.map(o => ({
    label: o.label,
    value: o.value,
    icon: o.icon,
  }));

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
