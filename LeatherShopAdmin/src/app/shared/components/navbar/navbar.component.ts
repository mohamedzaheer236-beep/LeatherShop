import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit, OnDestroy, inject } from '@angular/core';

import { MenuItem } from 'primeng/api';
import { MenubarModule } from 'primeng/menubar';
import { ButtonModule } from 'primeng/button';
import { TooltipModule } from 'primeng/tooltip';
import { BadgeModule } from 'primeng/badge';
import { OverlayPanelModule } from 'primeng/overlaypanel';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';
import { SignalRService, OrderNotification } from '../../../core/services/signalr.service';
import { NotificationService } from '../../services/notification.service';
import { TimeAgoPipe } from '../../pipes/time.pipes';

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [MenubarModule, ButtonModule, TooltipModule, BadgeModule, OverlayPanelModule, TimeAgoPipe],
  templateUrl: './navbar.component.html',
  styleUrl: './navbar.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NavbarComponent implements OnInit, OnDestroy {
  private auth = inject(AuthService);
  private signalR = inject(SignalRService);
  private notification = inject(NotificationService);
  private router = inject(Router);
  private cdr = inject(ChangeDetectorRef);

  items: MenuItem[] = [];
  username = '';
  notifications: OrderNotification[] = [];
  private subs: Subscription[] = [];

  ngOnInit(): void {
    this.username = this.auth.getUsername() || 'Admin';
    this.items = [
      { label: 'Dashboard', icon: 'pi pi-home', routerLink: '/dashboard', routerLinkActiveOptions: { exact: true } },
      { label: 'Products', icon: 'pi pi-box', routerLink: '/products', routerLinkActiveOptions: { exact: false } },
      { label: 'Orders', icon: 'pi pi-shopping-cart', routerLink: '/orders', routerLinkActiveOptions: { exact: true } },
      { label: 'Customers', icon: 'pi pi-users', routerLink: '/customers', routerLinkActiveOptions: { exact: true } },
      { label: 'Chat', icon: 'pi pi-comments', routerLink: '/chat', routerLinkActiveOptions: { exact: true } },
      {
        label: 'Broadcast',
        icon: 'pi pi-megaphone',
        routerLink: '/broadcast',
        routerLinkActiveOptions: { exact: true },
      },
    ];

    // Start SignalR and listen for order notifications
    this.signalR.start();
    this.subs.push(
      this.signalR.newOrder$.subscribe(order => {
        this.notifications = [order, ...this.notifications.slice(0, 19)];
        this.cdr.markForCheck();
      }),
      this.signalR.outboxFailed$.subscribe(event => {
        this.notification.error(
          `Message delivery failed for ${event.customerName}: ${event.context}. Go to Chat → Failed Messages to retry.`,
        );
      }),
    );
  }

  ngOnDestroy(): void {
    this.subs.forEach(s => s.unsubscribe());
  }

  clearNotifications(): void {
    this.notifications = [];
  }

  onNotificationClick(n: OrderNotification): void {
    this.notifications = this.notifications.filter(x => x !== n);
    this.router.navigate(['/orders']);
  }

  logout(): void {
    this.router.navigate(['/login'], { state: { fromLogout: true } }).then(async navigated => {
      if (navigated) {
        await this.signalR.stop();
        this.auth.serverLogout();
        this.auth.clearSession();
      }
    });
  }
}
