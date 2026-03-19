import { Directive, inject, OnDestroy } from '@angular/core';
import { Dropdown } from 'primeng/dropdown';
import { Subscription } from 'rxjs';

let idSeq = 0;

/**
 * Patches PrimeNG p-dropdown's internal filter and focus-trap inputs
 * with id/name attributes to suppress browser "form field should have
 * an id or name attribute" warnings.
 */
@Directive({
  selector: 'p-dropdown',
  standalone: true
})
export class DropdownAccessibilityDirective implements OnDestroy {
  private dropdown = inject(Dropdown);
  private sub: Subscription;
  private uid = `dd-${++idSeq}`;

  constructor() {
    this.sub = this.dropdown.onShow.subscribe(() => {
      setTimeout(() => this.patchInputs());
    });
  }

  private patchInputs(): void {
    const base = this.dropdown.inputId || this.uid;

    // Patch filter input
    const filter = this.dropdown.filterViewChild?.nativeElement as HTMLInputElement | undefined;
    if (filter && !filter.id) {
      filter.id = `${base}-filter`;
      filter.name = `${base}-filter`;
    }

    // Patch hidden focus-trap inputs in the overlay
    const overlay = this.dropdown.overlayViewChild?.overlayViewChild?.nativeElement as HTMLElement | undefined;
    if (overlay) {
      const traps = overlay.querySelectorAll<HTMLInputElement>('input[role="presentation"]');
      traps.forEach((el, i) => {
        if (!el.id) {
          el.id = `${base}-trap-${i}`;
          el.name = `${base}-trap-${i}`;
        }
      });
    }
  }

  ngOnDestroy(): void {
    this.sub.unsubscribe();
  }
}
