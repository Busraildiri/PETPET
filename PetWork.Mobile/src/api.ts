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

export type RegisterRequest = {
  username: string;
  email: string;
  password: string;
  confirmPassword: string;
  acceptTerms: boolean;
};

export type AuthResponse = {
  userId: number;
  username: string;
  token: string;
  expiresAt: string;
  message: string;
};

export type LoginRequest = {
  emailOrUsername: string;
  password: string;
  rememberMe: boolean;
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

async function readAuthResponse(response: Response, fallbackMessage: string): Promise<AuthResponse> {
  const payload = await response.json().catch(() => null) as {
    message?: string;
    title?: string;
    errors?: Record<string, string[]>;
  } | null;

  if (!response.ok) {
    const validationMessage = payload?.errors ? Object.values(payload.errors).flat()[0] : undefined;
    if (response.status === 429) throw new Error('Çok fazla deneme yapıldı. Lütfen bir dakika sonra tekrar dene.');
    throw new Error(validationMessage || payload?.message || payload?.title || fallbackMessage);
  }

  return payload as AuthResponse;
}

export async function registerUser(request: RegisterRequest): Promise<AuthResponse> {
  const response = await fetch(`${apiUrl}/api/mobile/auth/register`, {
    method: 'POST',
    headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  });

  return readAuthResponse(response, 'Kayıt işlemi tamamlanamadı.');
}

export async function loginUser(request: LoginRequest): Promise<AuthResponse> {
  const response = await fetch(`${apiUrl}/api/mobile/auth/login`, {
    method: 'POST',
    headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  });

  return readAuthResponse(response, 'Giriş işlemi tamamlanamadı.');
}
