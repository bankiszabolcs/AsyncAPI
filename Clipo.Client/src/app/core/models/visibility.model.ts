// A backend Visibility szótár ID-jaival egyezik (AsyncApi.Enums.Visibility).
export enum Visibility {
  Public = 1,
  Unlisted = 2,
  Private = 3,
}

export interface VisibilityOption {
  value: Visibility;
  icon: string;
  translationKey: string;
}

export const VISIBILITY_OPTIONS: readonly VisibilityOption[] = [
  { value: Visibility.Public,   icon: 'pi-globe', translationKey: 'public' },
  { value: Visibility.Unlisted, icon: 'pi-link',  translationKey: 'unlisted' },
  { value: Visibility.Private,  icon: 'pi-lock',  translationKey: 'private' },
];

export function visibilityOption(value: number): VisibilityOption | undefined {
  return VISIBILITY_OPTIONS.find(o => o.value === value);
}
