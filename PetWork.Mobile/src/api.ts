export type Story = {
  id: number; type: 'blog' | 'disease' | 'recipe' | string; title: string; category: string;
  excerpt: string; imagePath?: string | null; publishedAt: string;
};

export type Question = {
  id: number; title: string; category: string; username: string; answerCount: number; createdAt: string;
};

export type HomePayload = {
  featured: Story[]; blogs: Story[]; questions: Question[]; memberCount: number; questionCount: number;
};

const configuredUrl = process.env.EXPO_PUBLIC_API_URL?.trim().replace(/\/$/, '');
export const apiUrl = configuredUrl || 'http://localhost:5147';

export function mediaUrl(path?: string | null) {
  if (!path) return `${apiUrl}/img/petwork-community-hero-our-pets-v2.png`;
  if (/^https?:\/\//i.test(path)) return path;
  return `${apiUrl}/${path.replace(/^~?\//, '')}`;
}

export async function getHome(signal?: AbortSignal): Promise<HomePayload> {
  const response = await fetch(`${apiUrl}/api/mobile/home`, { headers: { Accept: 'application/json' }, signal });
  if (!response.ok) throw new Error(`PetWork API ${response.status} döndürdü.`);
  return response.json();
}
