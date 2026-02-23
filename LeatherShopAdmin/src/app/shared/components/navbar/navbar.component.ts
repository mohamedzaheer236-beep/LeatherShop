import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MenuItem } from 'primeng/api';
import { MenubarModule } from 'primeng/menubar';
import { ButtonModule } from 'primeng/button';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [CommonModule, MenubarModule, ButtonModule],
  template: `
    <p-menubar [model]="items" styleClass="navbar-menubar">
      <ng-template pTemplate="start">
        <span class="navbar-brand">
          <i class="pi pi-briefcase navbar-logo"></i>
          <span class="navbar-brand-text">Leather Shop Admin</span>
        </span>
      </ng-template>
      <ng-template pTemplate="end">
        <div class="navbar-end">
          <span class="navbar-user"><i class="pi pi-user"></i> {{ username }}</span>
          <button pButton icon="pi pi-sign-out" label="Logout"
                  class="p-button-text p-button-sm navbar-logout"
                  (click)="logout()"></button>
        </div>
      </ng-template>
    </p-menubar>
  `,
  styles: [`
    .navbar-end {
      display: flex;
      align-items: center;
      gap: 0.75rem;
    }
    .navbar-user {
      font-size: 0.85rem;
      color: #6b7280;
      display: flex;
      align-items: center;
      gap: 0.35rem;
    }
    .navbar-logout {
      color: #dc2626 !important;
      font-size: 0.85rem;
    }
    .navbar-logout:hover {
      background: rgba(220, 38, 38, 0.08) !important;
    }
  `]
})
export class NavbarComponent implements OnInit {
  items: MenuItem[] = [];
  username = '';

  constructor(private auth: AuthService) {}

  ngOnInit(): void {
    this.username = this.auth.getUsername() || 'Admin';
    this.items = [
      { label: 'Dashboard', icon: 'pi pi-home', routerLink: '/dashboard' },
      { label: 'Products', icon: 'pi pi-box', routerLink: '/products' },
      { label: 'Orders', icon: 'pi pi-shopping-cart', routerLink: '/orders' },
      { label: 'Customers', icon: 'pi pi-users', routerLink: '/customers' },
      { label: 'Broadcast', icon: 'pi pi-megaphone', routerLink: '/broadcast' }
    ];
  }

  logout(): void {
    this.auth.logout();
  }
}