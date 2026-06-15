import { Component, inject, input, OnInit, signal } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { AuthService } from '../../core/auth/auth.service';
import { CommentService } from '../../core/services/comment.service';
import { Comment } from '../../core/models/comment.model';
import { TimeAgoPipe } from '../pipes/time-ago.pipe';

@Component({
  selector: 'app-comments',
  imports: [TimeAgoPipe, NgTemplateOutlet],
  templateUrl: './comments.html',
})
export class Comments implements OnInit {
  readonly videoId = input.required<string>();

  readonly auth = inject(AuthService);
  private readonly commentService = inject(CommentService);

  readonly comments = signal<Comment[]>([]);
  readonly loading = signal(true);

  readonly newText = signal('');
  readonly replyingToId = signal<string | null>(null);
  readonly replyText = signal('');
  readonly editingId = signal<string | null>(null);
  readonly editText = signal('');
  readonly deletingId = signal<string | null>(null);

  ngOnInit(): void {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.commentService.getByVideoId(this.videoId()).subscribe({
      next: c => { this.comments.set(c); this.loading.set(false); },
      error: ()  => this.loading.set(false),
    });
  }

  submitNew(): void {
    const content = this.newText().trim();
    if (!content) return;
    this.commentService.create(this.videoId(), content).subscribe(() => {
      this.newText.set('');
      this.load();
    });
  }

  startReply(id: string): void {
    this.replyingToId.set(id);
    this.replyText.set('');
    this.editingId.set(null);
  }

  submitReply(parentId: string): void {
    const content = this.replyText().trim();
    if (!content) return;
    this.commentService.create(this.videoId(), content, parentId).subscribe(() => {
      this.replyingToId.set(null);
      this.load();
    });
  }

  startEdit(comment: Comment): void {
    this.editingId.set(comment.id);
    this.editText.set(comment.content);
    this.replyingToId.set(null);
  }

  saveEdit(id: string): void {
    const content = this.editText().trim();
    if (!content) return;
    this.commentService.update(id, content).subscribe(() => {
      this.editingId.set(null);
      this.load();
    });
  }

  startDelete(id: string): void {
    this.deletingId.set(id);
    this.replyingToId.set(null);
    this.editingId.set(null);
  }

  confirmDelete(id: string): void {
    this.commentService.delete(id).subscribe(() => {
      this.deletingId.set(null);
      this.load();
    });
  }

  cancelDelete(): void { this.deletingId.set(null); }

  isOwn(comment: Comment): boolean {
    return this.auth.profile()?.id === comment.user.id;
  }

  cancelReply(): void  { this.replyingToId.set(null); }
  cancelEdit(): void   { this.editingId.set(null); }

  onNewInput(e: Event): void   { this.newText.set((e.target as HTMLTextAreaElement).value); }
  onReplyInput(e: Event): void { this.replyText.set((e.target as HTMLTextAreaElement).value); }
  onEditInput(e: Event): void  { this.editText.set((e.target as HTMLTextAreaElement).value); }
}
