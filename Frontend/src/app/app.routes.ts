import { Routes } from '@angular/router';
import { identified } from './core/session';
import { SignIn } from './pages/sign-in';
import { Market } from './pages/market';
import { StockPage } from './pages/stock';
import { Directory } from './pages/directory';
import { Portfolio } from './pages/portfolio';
export const routes: Routes = [
  { path: 'sign-in', component: SignIn, title: 'Enter the exchange · Burse' },
  { path: 'market', component: Market, canActivate: [identified], title: 'Market · Burse' },
  {
    path: 'market/:stockId',
    component: StockPage,
    canActivate: [identified],
    title: 'Stock details · Burse',
  },
  { path: 'traders', component: Directory, canActivate: [identified], title: 'Traders · Burse' },
  {
    path: 'portfolio',
    component: Portfolio,
    canActivate: [identified],
    title: 'My portfolio · Burse',
  },
  { path: '', pathMatch: 'full', redirectTo: 'market' },
  { path: '**', redirectTo: 'market' },
];
