import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { MenuItem } from 'primeng/api';
import { MenubarModule } from 'primeng/menubar';

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [MenubarModule],
  template: `
    <p-menubar [model]="items" styleClass="navbar-menubar">
      <ng-template pTemplate="start">
        <span class="navbar-brand">
          <i class="pi pi-briefcase navbar-logo"></i>
          <span class="navbar-brand-text">Leather Shop Admin</span>
        </span>
      </ng-template>
    </p-menubar>
  `
})
export class NavbarComponent implements OnInit {
  items: MenuItem[] = [];

  constructor(private router: Router) {}

  ngOnInit(): void {
    this.items = [
      { label: 'Dashboard', icon: 'pi pi-home', routerLink: '/dashboard' },
      { label: 'Products', icon: 'pi pi-box', routerLink: '/products' },
      { label: 'Orders', icon: 'pi pi-shopping-cart', routerLink: '/orders' },
      { label: 'Customers', icon: 'pi pi-users', routerLink: '/customers' },
      { label: 'Broadcast', icon: 'pi pi-megaphone', routerLink: '/broadcast' }
    ];
  }
}