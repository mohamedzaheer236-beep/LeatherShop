import { Routes } from '@angular/router';
import { ProductListComponent } from './components/product-list/product-list.component';
import { ProductFormComponent } from './components/product-form/product-form.component';
import { unsavedChangesGuard } from '../../core/guards/unsaved-changes.guard';

export const productsRoutes: Routes = [
  { path: '', component: ProductListComponent },
  { path: 'new', component: ProductFormComponent, canDeactivate: [unsavedChangesGuard] },
  { path: 'edit/:id', component: ProductFormComponent, canDeactivate: [unsavedChangesGuard] }
];
