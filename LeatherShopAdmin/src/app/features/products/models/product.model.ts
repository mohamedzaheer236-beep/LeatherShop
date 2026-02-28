export interface Product {
  id: number;
  name: string;
  description: string;
  brand: string;
  category: string;
  price: number;
  stockQuantity: number;
  imageUrl: string;
  imageUrls: string[];
  isActive: boolean;
}

export interface CreateProduct {
  name: string;
  description: string;
  brand: string;
  category: string;
  price: number;
  stockQuantity: number;
  imageUrl: string;
  imageUrls?: string[];
}
