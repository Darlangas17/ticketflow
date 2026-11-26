import { inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { CanDeactivateFn } from '@angular/router';

import { confirm } from './components/confirm-dialog';

/** Implemented by pages with a form that should not be left half-filled by accident. */
export interface HasUnsavedChanges {
  hasUnsavedChanges(): boolean;
}

export const unsavedChangesGuard: CanDeactivateFn<HasUnsavedChanges> = (component) =>
  !component.hasUnsavedChanges() ||
  confirm(inject(MatDialog), {
    title: 'Discard changes?',
    message: 'You have unsaved changes. If you leave this page they will be lost.',
    confirmText: 'Discard',
    destructive: true,
  });
