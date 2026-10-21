import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

export function noDaysValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const days = control.get('days')?.value;

    if (!days) {
      return null;
    }

    const daysNum = parseInt(days, 10);

    if (daysNum < 0) {
      return { noDays: true };
    }

    return null;
  };
}
