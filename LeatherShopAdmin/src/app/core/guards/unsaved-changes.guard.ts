import { CanDeactivateFn } from '@angular/router';

export interface HasUnsavedChanges {
  canDeactivate(): boolean;
}

export const unsavedChangesGuard: CanDeactivateFn<HasUnsavedChanges> = (component) => {
  return component.canDeactivate ? component.canDeactivate() : true;
};
