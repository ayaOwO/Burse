export type Side = 'Bid' | 'Ask';
export type OrderStatus = 'Open' | 'Cancelled' | 'Filled';
export interface Stock {
  id: number;
  name: string;
  currentPrice: number;
  amount: number;
}
export interface Trader {
  id: number;
  name: string;
  money: number;
}
export interface Order {
  id: string;
  type: Side;
  price: number;
  amount: number;
  traderId: number;
  stockId: number;
  status: OrderStatus;
}
export interface Trade {
  id: number;
  stockId: number;
  buyerId: number;
  sellerId: number;
  amount: number;
  price: number;
  orderIds: string[];
}
export interface StockSummary {
  stock: Stock;
  orders: Order[];
  lastTrades: Trade[];
}
export interface Account {
  orders: Order[];
  stocksOwned: Record<string, number>;
  money: number;
}
export interface Performance {
  trades: Trade[];
  currentBalance: number;
  profitLoss: number;
}
export interface CreateOrder {
  trader_id: number;
  stock_id: number;
  type: Side;
  price: number;
  amount: number;
}
