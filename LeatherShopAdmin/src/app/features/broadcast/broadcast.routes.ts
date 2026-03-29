import { Routes } from '@angular/router';
import { BroadcastComponent } from './components/broadcast/broadcast.component';
import { BroadcastHistoryComponent } from './components/broadcast-history/broadcast-history.component';

export const broadcastRoutes: Routes = [
  { path: '', component: BroadcastComponent },
  { path: 'history', component: BroadcastHistoryComponent },
];
