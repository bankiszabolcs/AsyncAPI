import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { catchError, of } from 'rxjs';
import { Button } from 'primeng/button';
import { Dialog } from 'primeng/dialog';
import { InputText } from 'primeng/inputtext';
import { Select } from 'primeng/select';
import { Toast } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { PlaylistService } from '../../../core/services/playlist.service';
import { Playlist } from '../../../core/models/playlist.model';
import { VISIBILITY_OPTIONS } from '../../../core/models/visibility.model';

@Component({
  selector: 'app-playlist-list',
  imports: [FormsModule, Button, Dialog, InputText, Select, Toast],
  providers: [PlaylistService, MessageService],
  templateUrl: './playlist-list.html',
})
export class PlaylistList implements OnInit {
  private readonly playlistService = inject(PlaylistService);
  private readonly messageService  = inject(MessageService);
  private readonly router          = inject(Router);

  readonly playlists  = signal<Playlist[]>([]);
  readonly isLoading  = signal(true);

  readonly showFormDialog   = signal(false);
  readonly showDeleteDialog = signal(false);
  readonly isSaving         = signal(false);

  readonly editingId   = signal<string | null>(null);
  readonly formTitle   = signal('');
  readonly formDesc    = signal('');
  readonly formVis     = signal(3);
  readonly deletingId  = signal<string | null>(null);

  readonly visibilityOptions = VISIBILITY_OPTIONS.map(o => ({ label: o.label, value: o.value }));

  readonly isEdit = computed(() => this.editingId() !== null);

  ngOnInit(): void {
    this.loadPlaylists();
  }

  openCreate(): void {
    this.editingId.set(null);
    this.formTitle.set('');
    this.formDesc.set('');
    this.formVis.set(3);
    this.showFormDialog.set(true);
  }

  openEdit(p: Playlist, event: Event): void {
    event.stopPropagation();
    this.editingId.set(p.id);
    this.formTitle.set(p.title);
    this.formDesc.set(p.description ?? '');
    this.formVis.set(p.visibilityId);
    this.showFormDialog.set(true);
  }

  openDelete(p: Playlist, event: Event): void {
    event.stopPropagation();
    this.deletingId.set(p.id);
    this.showDeleteDialog.set(true);
  }

  save(): void {
    const title = this.formTitle().trim();
    if (!title) return;

    this.isSaving.set(true);
    const req = { title, description: this.formDesc().trim() || null, visibilityId: this.formVis() };

    const op = this.isEdit()
      ? this.playlistService.update(this.editingId()!, req)
      : this.playlistService.create(req);

    op.subscribe({
      next: () => {
        this.showFormDialog.set(false);
        this.isSaving.set(false);
        this.messageService.add({
          severity: 'success',
          summary: this.isEdit() ? 'Lista frissítve' : 'Lista létrehozva',
          life: 3000,
        });
        this.loadPlaylists();
      },
      error: () => {
        this.isSaving.set(false);
        this.messageService.add({ severity: 'error', summary: 'Hiba', detail: 'Nem sikerült menteni.', life: 4000 });
      },
    });
  }

  confirmDelete(): void {
    const id = this.deletingId();
    if (!id) return;

    this.playlistService.delete(id).subscribe({
      next: () => {
        this.showDeleteDialog.set(false);
        this.playlists.update(list => list.filter(p => p.id !== id));
        this.messageService.add({ severity: 'success', summary: 'Törölve', life: 3000 });
      },
      error: () => {
        this.messageService.add({ severity: 'error', summary: 'Hiba', detail: 'Nem sikerült törölni.', life: 4000 });
      },
    });
  }

  navigateToDetail(id: string): void {
    this.router.navigate(['/playlists', id]);
  }

  formatVideoCount(n: number): string {
    return n === 1 ? '1 videó' : `${n} videó`;
  }

  private loadPlaylists(): void {
    this.isLoading.set(true);
    this.playlistService.getMyPlaylists()
      .pipe(catchError(() => of([] as Playlist[])))
      .subscribe(list => {
        this.playlists.set(list);
        this.isLoading.set(false);
      });
  }
}
