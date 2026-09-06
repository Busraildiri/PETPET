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

export type QuestionSummary = {
  id: number;
  title: string;
  excerpt: string;
  category: string;
  username: string;
  answerCount: number;
  viewCount: number;
  hasAcceptedAnswer: boolean;
  createdAt: string;
};

export type QuestionsResponse = {
  items: QuestionSummary[];
  categories: string[];
  totalCount: number;
};

export type QuestionAnswer = {
  id: number;
  content: string;
  username: string;
  isAccepted: boolean;
  score: number;
  createdAt: string;
};

export type QuestionDetail = {
  id: number;
  title: string;
  content: string;
  category: string;
  tags?: string | null;
  username: string;
  viewCount: number;
  createdAt: string;
  answers: QuestionAnswer[];
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

export async function getQuestions(signal?: AbortSignal): Promise<QuestionsResponse> {
  const response = await fetch(`${apiUrl}/api/mobile/questions?take=50`, {
    headers: { Accept: 'application/json' }, signal,
  });
  if (!response.ok) throw new Error(`Sorular alınamadı (${response.status}).`);
  return response.json();
}

export async function getQuestionDetail(id: number, signal?: AbortSignal): Promise<QuestionDetail> {
  const response = await fetch(`${apiUrl}/api/mobile/questions/${id}`, {
    headers: { Accept: 'application/json' }, signal,
  });
  if (!response.ok) throw new Error(`Soru ayrıntısı alınamadı (${response.status}).`);
  return response.json();
}

export async function postQuestionAnswer(questionId: number, content: string): Promise<QuestionAnswer> {
  const stored = await SecureStore.getItemAsync('petim.session');
  if (!stored) throw new Error('Yanıt vermek için giriş yapmalısın.');

  let token: string | undefined;
  try { token = (JSON.parse(stored) as { token?: string }).token; } catch { token = undefined; }
  if (!token) throw new Error('Oturum bilgisi bulunamadı. Lütfen yeniden giriş yap.');

  const response = await fetch(`${apiUrl}/api/mobile/questions/${questionId}/answers`, {
    method: 'POST',
    headers: { Accept: 'application/json', 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
    body: JSON.stringify({ content }),
  });
  const payload = await response.json().catch(() => null) as { message?: string; title?: string; errors?: Record<string, string[]> } | QuestionAnswer | null;

  if (!response.ok) {
    const errorPayload = payload as { message?: string; title?: string; errors?: Record<string, string[]> } | null;
    const validationMessage = errorPayload?.errors ? Object.values(errorPayload.errors).flat()[0] : undefined;
    if (response.status === 429) throw new Error('Çok fazla yanıt gönderildi. Lütfen bir dakika sonra tekrar dene.');
    throw new Error(validationMessage || errorPayload?.message || errorPayload?.title || 'Yanıt gönderilemedi.');
  }

  return payload as QuestionAnswer;
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
import * as SecureStore from 'expo-secure-store';
