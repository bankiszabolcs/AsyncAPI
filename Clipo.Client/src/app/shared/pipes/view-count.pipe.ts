import { Pipe, PipeTransform } from '@angular/core';

@Pipe({ name: 'viewCount', standalone: true })
export class ViewCountPipe implements PipeTransform {
  transform(value: number | null | undefined): string {
    if (value == null) return '';

    if (value >= 1_000_000) {
      return this.fmt(value / 1_000_000) + ' M';
    }
    if (value >= 1_000) {
      return this.fmt(value / 1_000) + ' E';
    }
    return value.toString();
  }

  private fmt(n: number): string {
    const rounded = Math.round(n * 10) / 10;
    return Number.isInteger(rounded)
      ? rounded.toString()
      : rounded.toFixed(1).replace('.', ',');
  }
}
