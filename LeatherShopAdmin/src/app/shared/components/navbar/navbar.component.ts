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
          <span class="logo">&#128092;</span>
          <span class="brand-text">Leather Shop Admin</span>
        </span>
      </ng-template>
    </p-menubar>
  `,
  styles: [`
    :host ::ng-deep .navbar-menubar {
      background: #1a1a2e;
      border: none;
      border-radius: 0;
      padding: 0 24px;
      height: 60px;
      position: fixed;
      top: 0; left: 0; right: 0;
      z-index: 1000;
      box-shadow: 0 2px 8px rgba(0,0,0,0.15);
    }
    :host ::ng-deep .navbar-menubar .p-menubar-root-list > .p-menuitem > .p-menuitem-content {
      background: transparent;
      border-radius: 6px;
      transition: all 0.2s;
    }
    :host ::ng-deep .navbar-menubar .p-menubar-root-list > .p-menuitem > .p-menuitem-content:hover {
      background: rgba(255,255,255,0.08);
    }
    :host ::ng-deep .navbar-menubar .p-menubar-root-list > .p-menuitem > .p-menuitem-content .p-menuitem-link .p-menuitem-text {
      color: #b0b0c0;
      font-weight: 500;
      font-size: 14px;
    }
    :host ::ng-deep .navbar-menubar .p-menubar-root-list > .p-menuitem > .p-menuitem-content:hover .p-menuitem-link .p-menuitem-text {
      color: #fff;
    }
    :host ::ng-deep .navbar-menubar .p-menubar-root-list > .p-menuitem > .p-menuitem-content .p-menuitem-link .p-menuitem-icon {
      color: #b0b0c0;
    }
    :host ::ng-deep .navbar-menubar .p-menubar-root-list > .p-menuitem > .p-menuitem-content:hover .p-menuitem-link .p-menuitem-icon {
      color: #fff;
    }
    .navbar-brand {
      display: flex;
      align-items: center;
      gap: 10px;
      margin-right: 24px;
    }
    .logo { font-size: 28px; }
    .brand-text {
      color: #e0c097;
      font-size: 20px;
      font-weight: 700;
      letter-spacing: 0.5px;
    }
  `]
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
