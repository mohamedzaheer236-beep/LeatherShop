import { ChangeDetectionStrategy, ChangeDetectorRef, Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { CustomerService } from '../../services/customer.service';
import { CreateCustomer, CUSTOMER_CATEGORIES } from '../../models/customer.model';
import { NotificationService } from '../../../../shared/services/notification.service';

import { DialogModule } from 'primeng/dialog';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { ButtonModule } from 'primeng/button';

@Component({
  selector: 'app-customer-import-dialog',
  standalone: true,
  imports: [ReactiveFormsModule, DialogModule, InputTextareaModule, ButtonModule],
  templateUrl: './customer-import-dialog.component.html',
  styleUrl: './customer-import-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CustomerImportDialogComponent {
  private fb = inject(FormBuilder);
  private customerService = inject(CustomerService);
  private notification = inject(NotificationService);
  private cdr = inject(ChangeDetectorRef);

  @Input() visible = false;
  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() imported = new EventEmitter<void>();

  form = this.fb.group({
    importText: ['', [Validators.required]],
  });

  importing = false;

  onShow(): void {
    this.form.reset({ importText: '' });
  }

  submit(): void {
    if (this.form.invalid) {
      this.notification.error('Paste at least one phone number');
      return;
    }

    const importText = this.form.get('importText')?.value || '';
    const lines = importText
      .trim()
      .split('\n')
      .filter((l: string) => l.trim());
    if (lines.length === 0) {
      this.notification.error('Paste at least one phone number');
      return;
    }

    const phonePattern = /^\d{10,15}$/;
    const validCategoryValues = new Set(CUSTOMER_CATEGORIES.map(c => c.value.toLowerCase()));
    const validCustomers: CreateCustomer[] = [];
    const invalidLines: number[] = [];

    lines.forEach((line: string, index: number) => {
      const parts = line.split(',').map((p: string) => p.trim());
      const phone = parts[0];
      if (phonePattern.test(phone)) {
        // Parse optional category (3rd column): phone,name,category
        let category = 'FriendsAndFamily';
        if (parts[2]) {
          const matched = CUSTOMER_CATEGORIES.find(c => c.value.toLowerCase() === parts[2].toLowerCase());
          if (matched) category = matched.value;
        }
        validCustomers.push({ phoneNumber: phone, name: parts[1] || '', category });
      } else {
        invalidLines.push(index + 1);
      }
    });

    if (validCustomers.length === 0) {
      this.notification.error(`All ${lines.length} line(s) have invalid phone numbers. Phone must be 10-15 digits.`);
      return;
    }

    if (invalidLines.length > 0) {
      const lineNums =
        invalidLines.length <= 5
          ? invalidLines.join(', ')
          : invalidLines.slice(0, 5).join(', ') + `, ... (${invalidLines.length} total)`;
      this.notification.warning(
        `Skipping ${invalidLines.length} line(s) with invalid phone numbers (line ${lineNums}). Importing ${validCustomers.length} valid entries.`,
      );
    }

    this.importing = true;
    this.customerService.bulkImportCustomers(validCustomers).subscribe({
      next: res => {
        this.notification.success(`Imported ${res.imported} customers (${res.skippedDuplicates} duplicates skipped)`);
        this.importing = false;
        this.close();
        this.imported.emit();
        this.cdr.markForCheck();
      },
      error: () => {
        this.importing = false;
        this.cdr.markForCheck();
      },
    });
  }

  close(): void {
    this.visible = false;
    this.visibleChange.emit(false);
  }
}
