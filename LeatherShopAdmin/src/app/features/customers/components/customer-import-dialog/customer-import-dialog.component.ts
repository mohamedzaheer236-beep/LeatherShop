import { ChangeDetectionStrategy, ChangeDetectorRef, Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CustomerService } from '../../services/customer.service';
import { CreateCustomer, CUSTOMER_CATEGORIES } from '../../models/customer.model';
import { NotificationService } from '../../../../shared/services/notification.service';

import { DialogModule } from 'primeng/dialog';
import { ButtonModule } from 'primeng/button';
import { FileUploadModule } from 'primeng/fileupload';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';

import * as XLSX from 'xlsx';

interface ParsedRow {
  row: number;
  phoneNumber: string;
  name: string;
  address: string;
  category: string;
}

interface ValidationError {
  row: number;
  field: string;
  message: string;
}

const VALID_CATEGORIES = new Set(CUSTOMER_CATEGORIES.map(c => c.value.toLowerCase()));
const CATEGORY_MAP = new Map(CUSTOMER_CATEGORIES.map(c => [c.value.toLowerCase(), c.value]));

// Flexible column name matching
const COLUMN_ALIASES: Record<string, string[]> = {
  phonenumber: ['phonenumber', 'phone number', 'phone', 'phone_number', 'mobile', 'mobilenumber', 'mobile number'],
  name: ['name', 'customer name', 'customername', 'customer_name'],
  address: ['address', 'shipping address', 'shippingaddress', 'shipping_address'],
  category: ['category', 'type', 'customer category', 'customercategory', 'customer_category'],
};

@Component({
  selector: 'app-customer-import-dialog',
  standalone: true,
  imports: [CommonModule, DialogModule, ButtonModule, FileUploadModule, TableModule, TagModule],
  templateUrl: './customer-import-dialog.component.html',
  styleUrl: './customer-import-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CustomerImportDialogComponent {
  private customerService = inject(CustomerService);
  private notification = inject(NotificationService);
  cdr = inject(ChangeDetectorRef);

  @Input() visible = false;
  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() imported = new EventEmitter<void>();

  // State
  fileName = '';
  parsedRows: ParsedRow[] = [];
  errors: ValidationError[] = [];
  importing = false;
  validating = false;
  validated = false;
  fileError = '';

  get hasErrors(): boolean {
    return this.errors.length > 0 || !!this.fileError;
  }

  get canImport(): boolean {
    return this.validated && !this.hasErrors && this.parsedRows.length > 0 && !this.importing;
  }

  onShow(): void {
    this.reset();
  }

  downloadTemplate(): void {
    const ws = XLSX.utils.aoa_to_sheet([['PhoneNumber', 'Name', 'Address', 'Category']]);
    ws['!cols'] = [{ wch: 18 }, { wch: 20 }, { wch: 35 }, { wch: 20 }];
    const wb = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(wb, ws, 'Customers');
    XLSX.writeFile(wb, 'customer_import_template.xlsx');
  }

  reset(): void {
    this.fileName = '';
    this.parsedRows = [];
    this.errors = [];
    this.importing = false;
    this.validating = false;
    this.validated = false;
    this.fileError = '';
  }

  onFileChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files?.[0]) {
      this.onFileSelect({ files: [input.files[0]] });
    }
  }

  onFileDrop(event: DragEvent): void {
    const file = event.dataTransfer?.files?.[0];
    if (file) {
      this.onFileSelect({ files: [file] });
    }
  }

  onFileSelect(event: { files: File[] }): void {
    const file: File | undefined = event.files?.[0];
    if (!file) return;

    this.reset();
    this.fileName = file.name;

    // Validate file extension
    const ext = file.name.split('.').pop()?.toLowerCase();
    if (ext !== 'xlsx' && ext !== 'xls') {
      this.fileError = `Invalid file format ".${ext}". Please upload an Excel file (.xlsx or .xls).`;
      this.cdr.markForCheck();
      return;
    }

    // Validate file size (max 5MB)
    if (file.size > 5 * 1024 * 1024) {
      this.fileError = 'File size exceeds 5MB. Please use a smaller file.';
      this.cdr.markForCheck();
      return;
    }

    this.validating = true;
    this.cdr.markForCheck();

    const reader = new FileReader();
    reader.onload = (e: ProgressEvent<FileReader>) => {
      try {
        const data = new Uint8Array(e.target?.result as ArrayBuffer);
        const workbook = XLSX.read(data, { type: 'array' });
        const sheetName = workbook.SheetNames[0];
        if (!sheetName) {
          this.fileError = 'Excel file has no sheets.';
          this.validating = false;
          this.cdr.markForCheck();
          return;
        }

        const sheet = workbook.Sheets[sheetName];
        const jsonData: Record<string, unknown>[] = XLSX.utils.sheet_to_json(sheet, { defval: '' });

        if (jsonData.length === 0) {
          this.fileError = 'Excel file has no data rows. Please add at least one customer.';
          this.validating = false;
          this.cdr.markForCheck();
          return;
        }

        if (jsonData.length > 1000) {
          this.fileError = `Too many rows (${jsonData.length}). Maximum 1000 customers per import.`;
          this.validating = false;
          this.cdr.markForCheck();
          return;
        }

        // Map column names
        const headers = Object.keys(jsonData[0]);
        const columnMap = this.mapColumns(headers);
        if (!columnMap) {
          this.validating = false;
          this.cdr.markForCheck();
          return;
        }

        // Parse rows
        this.parsedRows = jsonData.map((row, i) => ({
          row: i + 2, // Excel row (1-indexed header + data)
          phoneNumber: String(row[columnMap['phonenumber']] ?? '').trim(),
          name: String(row[columnMap['name']] ?? '').trim(),
          address: String(row[columnMap['address']] ?? '').trim(),
          category: String(row[columnMap['category']] ?? '').trim(),
        }));

        // Run local validation
        this.validateRows();

        // If local validation passes, check phones against DB
        if (this.errors.length === 0) {
          this.checkPhonesInDb();
        } else {
          this.validating = false;
          this.validated = true;
          this.cdr.markForCheck();
        }
      } catch {
        this.fileError = 'Failed to read the Excel file. Please ensure it is a valid .xlsx or .xls file.';
        this.validating = false;
        this.cdr.markForCheck();
      }
    };
    reader.readAsArrayBuffer(file);
  }

  private mapColumns(headers: string[]): Record<string, string> | null {
    const normalized = headers.map(h => ({ original: h, lower: h.toLowerCase().trim() }));
    const result: Record<string, string> = {};
    const requiredFields = ['phonenumber', 'name', 'address', 'category'];
    const missing: string[] = [];
    const allKnownAliases = new Set(Object.values(COLUMN_ALIASES).flat());

    for (const field of requiredFields) {
      const aliases = COLUMN_ALIASES[field];
      const match = normalized.find(h => aliases.includes(h.lower));
      if (match) {
        result[field] = match.original;
      } else {
        missing.push(field === 'phonenumber' ? 'PhoneNumber' : field.charAt(0).toUpperCase() + field.slice(1));
      }
    }

    if (missing.length > 0) {
      this.fileError = `Missing required column(s): ${missing.join(', ')}. ` +
        `Your file has columns: ${headers.join(', ')}. ` +
        `Expected: PhoneNumber, Name, Address, Category.`;
      return null;
    }

    // Check for unexpected extra columns
    const extraColumns = normalized
      .filter(h => !allKnownAliases.has(h.lower))
      .map(h => h.original);

    if (extraColumns.length > 0) {
      this.fileError = `Unexpected column(s): ${extraColumns.join(', ')}. ` +
        `Only these columns are allowed: PhoneNumber, Name, Address, Category. ` +
        `Please remove the extra column(s) and re-upload.`;
      return null;
    }

    return result;
  }

  private validateRows(): void {
    this.errors = [];
    const phonesSeen = new Map<string, number>(); // phone -> first row

    for (const row of this.parsedRows) {
      // Phone required
      if (!row.phoneNumber) {
        this.errors.push({ row: row.row, field: 'PhoneNumber', message: 'Phone number is empty.' });
        continue;
      }

      // Phone format
      if (!/^\d{10,15}$/.test(row.phoneNumber)) {
        this.errors.push({
          row: row.row,
          field: 'PhoneNumber',
          message: `Phone "${row.phoneNumber}" is invalid. Must be 10-15 digits only.`,
        });
        continue;
      }

      // Duplicate within file
      const firstSeen = phonesSeen.get(row.phoneNumber);
      if (firstSeen !== undefined) {
        this.errors.push({
          row: row.row,
          field: 'PhoneNumber',
          message: `Phone "${row.phoneNumber}" is duplicated (first appears in row ${firstSeen}).`,
        });
      } else {
        phonesSeen.set(row.phoneNumber, row.row);
      }

      // Category validation
      if (row.category) {
        if (!VALID_CATEGORIES.has(row.category.toLowerCase())) {
          this.errors.push({
            row: row.row,
            field: 'Category',
            message: `Category "${row.category}" is invalid. Must be: Reseller, DirectCorporate, or FriendsAndFamily.`,
          });
        }
      }
    }
  }

  private checkPhonesInDb(): void {
    const phones = this.parsedRows.map(r => r.phoneNumber).filter(p => /^\d{10,15}$/.test(p));
    if (phones.length === 0) {
      this.validating = false;
      this.validated = true;
      this.cdr.markForCheck();
      return;
    }

    this.customerService.checkPhonesExist(phones).subscribe({
      next: existing => {
        if (existing.length > 0) {
          const existingSet = new Set(existing);
          for (const row of this.parsedRows) {
            if (existingSet.has(row.phoneNumber)) {
              this.errors.push({
                row: row.row,
                field: 'PhoneNumber',
                message: `Phone "${row.phoneNumber}" already exists in the database.`,
              });
            }
          }
        }
        this.validating = false;
        this.validated = true;
        this.cdr.markForCheck();
      },
      error: () => {
        this.fileError = 'Failed to verify phone numbers against the database. Please try again.';
        this.validating = false;
        this.cdr.markForCheck();
      },
    });
  }

  submit(): void {
    if (!this.canImport) return;

    this.importing = true;
    this.cdr.markForCheck();

    const customers: CreateCustomer[] = this.parsedRows.map(r => ({
      phoneNumber: r.phoneNumber,
      name: r.name || undefined,
      address: r.address || undefined,
      category: CATEGORY_MAP.get(r.category.toLowerCase()) || 'FriendsAndFamily',
    }));

    this.customerService.bulkImportCustomers(customers).subscribe({
      next: res => {
        this.notification.success(`Successfully imported ${res.imported} customer(s)!`);
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

  getCategorySeverity(category: string): 'info' | 'secondary' | 'warning' | undefined {
    switch (category.toLowerCase()) {
      case 'reseller': return 'info';
      case 'directcorporate': return 'secondary';
      case 'friendsandfamily': return 'warning';
      default: return undefined;
    }
  }

  getCategoryLabel(value: string): string {
    const found = CUSTOMER_CATEGORIES.find(c => c.value.toLowerCase() === value.toLowerCase());
    return found?.label || value;
  }

  close(): void {
    this.visible = false;
    this.visibleChange.emit(false);
  }
}
