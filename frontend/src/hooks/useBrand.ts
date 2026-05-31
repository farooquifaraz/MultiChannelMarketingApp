import { useEffect, useState } from 'react';
import { fetchBrand, getCachedBrand, brandDefaults, type BrandInfo } from '../config/brand';

const API_BASE = (import.meta as any).env?.VITE_API_URL || 'http://localhost:5211/api/v1';

/**
 * Subscribe to the platform branding. First call hits the public endpoint;
 * subsequent calls return the cached value synchronously.
 */
export function useBrand(): BrandInfo {
  const [brand, setBrand] = useState<BrandInfo>(getCachedBrand());

  useEffect(() => {
    let alive = true;
    fetchBrand(API_BASE).then(b => { if (alive) setBrand(b); });
    return () => { alive = false; };
  }, []);

  return brand ?? brandDefaults;
}
