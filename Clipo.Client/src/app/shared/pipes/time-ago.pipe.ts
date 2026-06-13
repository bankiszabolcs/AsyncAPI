import { Pipe, PipeTransform } from '@angular/core';

@Pipe({ name: 'timeAgo', standalone: true })
export class TimeAgoPipe implements PipeTransform {
  transform(value: string | Date | null | undefined): string {
    if (!value) return '';
    const diff = Date.now() - new Date(value).getTime();
    const mins  = Math.floor(diff / 60_000);
    const hours = Math.floor(diff / 3_600_000);
    const days  = Math.floor(diff / 86_400_000);
    const weeks = Math.floor(days / 7);
    const months = Math.floor(days / 30);
    const years  = Math.floor(days / 365);

    if (years  >= 1) return years  === 1 ? '1 éve'          : `${years} éve`;
    if (months >= 1) return months === 1 ? '1 hónapja'      : `${months} hónapja`;
    if (weeks  >= 1) return weeks  === 1 ? '1 hete'         : `${weeks} hete`;
    if (days   >= 1) return days   === 1 ? '1 napja'        : `${days} napja`;
    if (hours  >= 1) return hours  === 1 ? '1 órája'        : `${hours} órája`;
    if (mins   >= 1) return mins   === 1 ? '1 perce'        : `${mins} perce`;
    return 'most';
  }
}
