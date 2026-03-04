import { FormGroup } from '@angular/forms';

/**
 * Checks whether a form control is invalid and should show validation errors.
 * A field is considered "invalid" when it is both invalid AND has been
 * interacted with (dirty/touched) or the form has been submitted.
 */
export function isFieldInvalid(form: FormGroup, field: string, submitted: boolean): boolean {
  const control = form.get(field);
  return !!(control && control.invalid && (control.dirty || control.touched || submitted));
}
