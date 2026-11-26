// Mirrors the API contracts in TicketFlow.Application/Catalogs.

export interface Branch {
  id: number;
  name: string;
  city: string;
  isActive: boolean;
}

export interface Category {
  id: number;
  name: string;
  description: string | null;
}
