import {
  Component,
  ElementRef,
  HostListener,
  OnDestroy,
  OnInit,
  inject,
  output,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Subject, Subscription, of, switchMap, debounceTime, distinctUntilChanged, filter } from 'rxjs';
import { NavigationEnd } from '@angular/router';
import { VideoService } from '../../core/services/video.service';

const HISTORY_KEY = 'clipo_search_history';
const MAX_HISTORY = 10;

@Component({
  selector: 'app-search-bar',
  imports: [FormsModule],
  templateUrl: './search-bar.html',
})
export class SearchBar implements OnInit, OnDestroy {
  readonly searched = output<void>();

  private readonly router = inject(Router);
  private readonly videoService = inject(VideoService);
  private readonly el = inject(ElementRef);

  readonly query = signal('');
  readonly dropdownVisible = signal(false);
  readonly suggestions = signal<string[]>([]);
  readonly history = signal<string[]>([]);

  private readonly input$ = new Subject<string>();
  private readonly subs = new Subscription();

  ngOnInit(): void {
    this.history.set(this.loadHistory());

    this.subs.add(this.input$.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      switchMap(q => q.length >= 2 ? this.videoService.getSuggestions(q) : of([])),
    ).subscribe(results => this.suggestions.set(results)));

    this.subs.add(this.router.events.pipe(
      filter(e => e instanceof NavigationEnd),
    ).subscribe((e: NavigationEnd) => {
      if (!e.urlAfterRedirects.startsWith('/search')) {
        this.query.set('');
        this.suggestions.set([]);
        this.dropdownVisible.set(false);
      }
    }));
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  onInput(value: string): void {
    this.query.set(value);
    this.input$.next(value);
    if (!value) this.suggestions.set([]);
  }

  onFocus(): void {
    this.dropdownVisible.set(true);
  }

  onEnter(): void {
    this.navigate(this.query());
  }

  selectSuggestion(title: string): void {
    this.query.set(title);
    this.navigate(title);
  }

  selectHistory(term: string): void {
    this.query.set(term);
    this.navigate(term);
  }

  removeHistoryItem(item: string, event: Event): void {
    event.stopPropagation();
    const updated = this.loadHistory().filter(h => h !== item);
    localStorage.setItem(HISTORY_KEY, JSON.stringify(updated));
    this.history.set(updated);
  }

  clearHistory(event: Event): void {
    event.stopPropagation();
    localStorage.removeItem(HISTORY_KEY);
    this.history.set([]);
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: Event): void {
    if (!this.el.nativeElement.contains(event.target)) {
      this.dropdownVisible.set(false);
    }
  }

  get showHistory(): boolean {
    return this.dropdownVisible() && !this.query() && this.history().length > 0;
  }

  get showSuggestions(): boolean {
    return this.dropdownVisible() && !!this.query() && this.suggestions().length > 0;
  }

  private navigate(term: string): void {
    if (!term.trim()) return;
    this.saveHistory(term.trim());
    this.history.set(this.loadHistory());
    this.dropdownVisible.set(false);
    this.searched.emit();
    this.router.navigate(['/search'], { queryParams: { q: term.trim() } });
  }

  private loadHistory(): string[] {
    try {
      return JSON.parse(localStorage.getItem(HISTORY_KEY) ?? '[]');
    } catch {
      return [];
    }
  }

  private saveHistory(term: string): void {
    const history = this.loadHistory().filter(h => h !== term);
    history.unshift(term);
    localStorage.setItem(HISTORY_KEY, JSON.stringify(history.slice(0, MAX_HISTORY)));
  }
}
