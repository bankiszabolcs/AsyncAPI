// A backend Visibility szótár ID-jaival egyezik (AsyncApi.Enums.Visibility).
export enum Visibility {
  Public = 1,
  Unlisted = 2,
  Private = 3,
}

export interface VisibilityOption {
  value: Visibility;
  label: string;
  icon: string;
  hint: string;
}

export const VISIBILITY_OPTIONS: readonly VisibilityOption[] = [
  { value: Visibility.Public,   label: 'Public',   icon: 'pi-globe', hint: 'Anyone can search for and watch this video.' },
  { value: Visibility.Unlisted, label: 'Unlisted', icon: 'pi-link',  hint: 'Anyone with the link can watch — it is not listed or searchable.' },
  { value: Visibility.Private,  label: 'Private',  icon: 'pi-lock',  hint: 'Only you can watch this video.' },
];

export function visibilityOption(value: number): VisibilityOption | undefined {
  return VISIBILITY_OPTIONS.find(o => o.value === value);
}
