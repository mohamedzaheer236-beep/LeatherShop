export interface ProductImageItem {
  id: number;
  url: string;
}

export interface Product {
  id: number;
  name: string;
  description: string;
  brand: string;
  category: string;
  price: number;
  stockQuantity: number;
  imageUrl: string;
  videoUrl?: string;
  imageUrls: string[];
  imageItems: ProductImageItem[];
  isActive: boolean;
  createdAt: string;
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
  videoUrl?: string;
}
