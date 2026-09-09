import { File } from 'expo-file-system';

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

export type ContentKind = 'all' | 'guides' | 'diseases' | 'recipes' | 'blogs' | 'grief';

export type ContentItem = {
  id: number; kind: ContentKind; title: string; summary: string; body: string; category: string;
  animalType?: string | null; meta?: string | null; imagePath?: string | null; publishedAt: string;
  viewCount: number; sourceUrl?: string | null; sourceName?: string | null; attribution?: string | null;
};

export type RegisterRequest = {
  username: string;
  email: string;
  password: string;
  confirmPassword: string;
  acceptTerms: boolean;
  rememberMe?: boolean;
};

export type AuthResponse = {
  userId: number;
  username: string;
  token: string;
  expiresAt: string;
  refreshToken: string;
  refreshExpiresAt: string;
  message: string;
};

export type EmailVerificationChallengeResponse = {
  requiresEmailVerification: true;
  challengeToken: string;
  expiresAt: string;
  maskedEmail: string;
  message: string;
};

export type CurrentUser = { userId: number; username: string; email: string };

export class ApiError extends Error {
  constructor(message: string, public readonly status: number) { super(message); }
}

export type LoginRequest = {
  emailOrUsername: string;
  password: string;
  rememberMe: boolean;
};

export type PetProfile = {
  id: number;
  name: string;
  type: string;
  breed?: string | null;
  age?: number | null;
  gender?: string | null;
  description?: string | null;
  profileImage?: string | null;
};

export type PatiMatchMyPet = PetProfile & {
  isActive: boolean;
  purpose?: 'friendship' | 'mate' | null;
  city?: string | null;
  district?: string | null;
  preferredTypes: string[];
};

export type PatiMatchCandidate = {
  petId: number;
  name: string;
  type: string;
  breed?: string | null;
  age?: number | null;
  gender?: string | null;
  description?: string | null;
  profileImage?: string | null;
  purpose: 'friendship' | 'mate';
  city: string;
  district?: string | null;
};

export type PatiMatchOverview = { pets: PatiMatchMyPet[]; matchCount: number };

export type PatiMatchMessage = {
  id: number;
  body: string;
  isMine: boolean;
  username: string;
  createdAt: string;
};

export type LocationSuggestion = {
  id: string;
  name: string;
  secondaryText?: string | null;
  label: string;
};

export type PetProfileRequest = Omit<PetProfile, 'id' | 'profileImage'>;

export type SocialPostPayload = {
  id: number;
  username: string;
  isAdmin: boolean;
  body: string;
  tags?: string | null;
  imagePath?: string | null;
  createdAt: string;
  commentCount: number;
  likeCount: number;
  isLikedByMe: boolean;
  isSavedByMe: boolean;
};

export type SocialCommentPayload = {
  id: number;
  username: string;
  isAdmin: boolean;
  body: string;
  createdAt: string;
  likeCount: number;
  isLikedByMe: boolean;
};

export type SocialReactionPayload = { active: boolean; count: number };

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

export type NearbyVeterinarian = {
  id: string;
  name: string;
  address?: string | null;
  distanceMeters?: number | null;
  rating?: number | null;
  userRatingCount?: number | null;
  openNow?: boolean | null;
  googleMapsUri?: string | null;
  latitude: number;
  longitude: number;
};

export type LostPetSummary = {
  id: number; kind: 'lost' | 'found'; petName: string; species: string; breed?: string | null;
  city: string; district: string; neighborhood?: string | null; latitude?: number | null; longitude?: number | null;
  eventAt: string; imagePath: string; status: 'active' | 'resolved' | 'closed'; createdAt: string;
  sightingCount: number; distanceKm?: number | null;
};

export type LostPetSighting = {
  id: number; locationLabel: string; seenAt: string; note?: string | null;
  latitude?: number | null; longitude?: number | null; createdAt: string;
};

export type LostPetDetail = Omit<LostPetSummary, 'sightingCount' | 'distanceKm'> & {
  distinguishingFeatures: string; collarOrMicrochip?: string | null; notes?: string | null;
  sourceName: string; expiresAt: string; ownerUsername: string; isMine: boolean; sightings: LostPetSighting[];
};

export type CreateLostPetRequest = {
  kind: 'lost' | 'found'; petName: string; species: string; breed?: string; distinguishingFeatures: string;
  eventAt: string; city: string; district: string; neighborhood?: string; latitude?: number; longitude?: number;
  collarOrMicrochip?: string; notes?: string;
  image: { uri: string; mimeType?: string | null };
};

function resolveApiUrl(value?: string) {
  const normalized = value?.trim().replace(/\/+$/, '') || (__DEV__ ? 'http://localhost:5147' : '');
  if (!normalized) throw new Error('Production API adresi tanımlanmamış.');
  let parsed: URL;
  try { parsed = new URL(normalized); } catch { throw new Error('API adresi geçerli bir URL değil.'); }
  if (!__DEV__ && parsed.protocol !== 'https:') throw new Error('Production API adresi HTTPS olmalıdır.');
  return normalized;
}
export const apiUrl = resolveApiUrl(process.env.EXPO_PUBLIC_API_URL);

async function fetchApi(input: RequestInfo | URL, init: RequestInit = {}, timeoutMs = 12_000) {
  const controller = new AbortController();
  const callerSignal = init.signal;
  let timedOut = false;
  const abortFromCaller = () => controller.abort();
  if (callerSignal?.aborted) controller.abort();
  else callerSignal?.addEventListener('abort', abortFromCaller, { once: true });
  const timer = setTimeout(() => { timedOut = true; controller.abort(); }, timeoutMs);

  try {
    return await fetch(input, { ...init, signal: controller.signal });
  } catch (reason) {
    if (timedOut) {
      const error = new Error('Sunucu yanıt vermedi. Bağlantını kontrol edip tekrar dene.');
      error.name = 'AbortError';
      throw error;
    }
    throw reason;
  } finally {
    clearTimeout(timer);
    callerSignal?.removeEventListener('abort', abortFromCaller);
  }
}

export function mediaUrl(path?: string | null) {
  if (!path) return `${apiUrl}/img/petwork-community-hero-our-pets-v2.png`;
  if (/^https?:\/\//i.test(path)) return path;
  return `${apiUrl}/${path.replace(/^~?\//, '')}`;
}

export async function getHome(signal?: AbortSignal): Promise<HomePayload> {
  const response = await fetchApi(`${apiUrl}/api/mobile/home`, { headers: { Accept: 'application/json' }, signal });
  if (!response.ok) throw new Error(`PetWork API ${response.status} döndürdü.`);
  return response.json();
}

export async function getContent(kind: ContentKind, search = '', signal?: AbortSignal): Promise<ContentItem[]> {
  const query = search.trim() ? `?search=${encodeURIComponent(search.trim())}` : '';
  const response = await fetchApi(`${apiUrl}/api/mobile/content/${kind}${query}`, {
    headers: { Accept: 'application/json' }, signal,
  });
  if (!response.ok) throw new Error(`İçerikler alınamadı (${response.status}).`);
  return response.json();
}

export async function registerUser(request: RegisterRequest): Promise<AuthResponse | EmailVerificationChallengeResponse> {
  const response = await authRequest('register', {
    method: 'POST',
    headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  }, 'Kayıt işlemi tamamlanamadı.');
  return response.json();
}

export async function loginUser(request: LoginRequest): Promise<AuthResponse | EmailVerificationChallengeResponse> {
  const response = await authRequest('login', {
    method: 'POST',
    headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  }, 'Giriş işlemi tamamlanamadı.');
  return response.json();
}

export async function verifyEmail(challengeToken: string, code: string): Promise<AuthResponse> {
  const response = await authRequest('verify-email', {
    method: 'POST', headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
    body: JSON.stringify({ challengeToken, code }),
  }, 'Doğrulama kodu kabul edilmedi.');
  return response.json();
}

export async function resendEmailCode(challengeToken: string): Promise<EmailVerificationChallengeResponse> {
  const response = await authRequest('resend-email-code', {
    method: 'POST', headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
    body: JSON.stringify({ challengeToken }),
  }, 'Doğrulama kodu yeniden gönderilemedi.');
  return response.json();
}

async function authRequest(path: string, init: RequestInit, fallback: string): Promise<Response> {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), 12000);
  try {
    const response = await fetchApi(`${apiUrl}/api/mobile/auth/${path}`, { ...init, signal: controller.signal });
    if (!response.ok) {
      const responseText = await response.text().catch(() => '');
      let payload: { message?: string; title?: string; errors?: Record<string, string[]> } | null = null;
      try { payload = responseText ? JSON.parse(responseText) : null; } catch { /* The development exception page may be plain text or HTML. */ }
      const validation = payload?.errors ? Object.values(payload.errors).flat()[0] : undefined;
      if (__DEV__) {
        console.error('[PetWork auth API]', {
          path,
          url: `${apiUrl}/api/mobile/auth/${path}`,
          status: response.status,
          statusText: response.statusText,
          contentType: response.headers.get('content-type'),
          response: responseText.slice(0, 2000),
        });
      }
      if (response.status === 429) throw new ApiError('Çok fazla deneme yapıldı. Lütfen bir dakika sonra tekrar dene.', response.status);
      const serverMessage = validation || payload?.message || payload?.title || fallback;
      throw new ApiError(__DEV__ ? `${serverMessage} (HTTP ${response.status})` : serverMessage, response.status);
    }
    return response;
  } catch (reason) {
    if (reason instanceof ApiError) throw reason;
    if (__DEV__) console.error('[PetWork auth transport]', { path, url: `${apiUrl}/api/mobile/auth/${path}`, reason });
    if (reason instanceof Error && reason.name === 'AbortError') throw new Error('Sunucu yanıt vermedi. Bağlantını kontrol edip tekrar dene.');
    throw new Error('Sunucuya bağlanılamadı. İnternet ve API adresini kontrol et.');
  } finally { clearTimeout(timer); }
}

export async function getCurrentUser(token: string): Promise<CurrentUser> {
  const response = await authRequest('me', { headers: { Accept: 'application/json', Authorization: `Bearer ${token}` } }, 'Oturum doğrulanamadı.');
  return response.json();
}

export async function refreshAuthSession(refreshToken: string): Promise<AuthResponse> {
  const response = await authRequest('refresh', {
    method: 'POST', headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
    body: JSON.stringify({ refreshToken }),
  }, 'Oturum yenilenemedi. Lütfen yeniden giriş yap.');
  return response.json();
}

export async function changePassword(token: string, currentPassword: string, newPassword: string): Promise<AuthResponse> {
  const response = await authRequest('change-password', {
    method: 'POST', headers: { Accept: 'application/json', 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
    body: JSON.stringify({ currentPassword, newPassword }),
  }, 'Şifre değiştirilemedi.');
  return response.json();
}

export async function forgotPassword(email: string): Promise<string> {
  const response = await authRequest('forgot-password', {
    method: 'POST', headers: { Accept: 'application/json', 'Content-Type': 'application/json' }, body: JSON.stringify({ email }),
  }, 'Şifre yenileme isteği gönderilemedi.');
  return ((await response.json()) as { message: string }).message;
}

export async function resetPassword(token: string, newPassword: string): Promise<string> {
  const response = await authRequest('reset-password', {
    method: 'POST', headers: { Accept: 'application/json', 'Content-Type': 'application/json' }, body: JSON.stringify({ token, newPassword }),
  }, 'Şifre yenilenemedi.');
  return ((await response.json()) as { message: string }).message;
}

export async function logoutSession(token: string): Promise<void> {
  await authRequest('logout', { method: 'POST', headers: { Authorization: `Bearer ${token}` } }, 'Oturum sunucuda kapatılamadı.');
}

export async function deleteAccount(token: string, password: string, confirmation: string): Promise<void> {
  await authRequest('account', {
    method: 'DELETE', headers: { Accept: 'application/json', 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
    body: JSON.stringify({ password, confirmation }),
  }, 'Hesap silinemedi.');
}

async function readPetResponse(response: Response, fallback: string): Promise<PetProfile> {
  const payload = await response.json().catch(() => null) as PetProfile | { message?: string; title?: string; errors?: Record<string, string[]> } | null;
  if (!response.ok) {
    const error = payload as { message?: string; title?: string; errors?: Record<string, string[]> } | null;
    const validation = error?.errors ? Object.values(error.errors).flat()[0] : undefined;
    throw new Error(validation || error?.message || error?.title || fallback);
  }
  return payload as PetProfile;
}

export async function getPets(token: string, signal?: AbortSignal): Promise<PetProfile[]> {
  const response = await fetchApi(`${apiUrl}/api/mobile/pets`, {
    headers: { Accept: 'application/json', Authorization: `Bearer ${token}` },
    signal,
  });
  if (!response.ok) {
    const payload = await response.json().catch(() => null) as { message?: string } | null;
    throw new Error(payload?.message || `Patiler yüklenemedi (${response.status}).`);
  }
  return response.json();
}

export async function savePet(token: string, request: PetProfileRequest, id?: number): Promise<PetProfile> {
  const response = await fetchApi(`${apiUrl}/api/mobile/pets${id ? `/${id}` : ''}`, {
    method: id ? 'PUT' : 'POST',
    headers: { Accept: 'application/json', 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
    body: JSON.stringify(request),
  });
  return readPetResponse(response, id ? 'Pati profili güncellenemedi.' : 'Pati profili oluşturulamadı.');
}

export async function deletePet(token: string, id: number): Promise<void> {
  const response = await fetchApi(`${apiUrl}/api/mobile/pets/${id}`, {
    method: 'DELETE',
    headers: { Accept: 'application/json', Authorization: `Bearer ${token}` },
  });
  if (!response.ok) {
    const payload = await response.json().catch(() => null) as { message?: string } | null;
    throw new Error(payload?.message || 'Pati profili silinemedi.');
  }
}

async function fetchSocialPostsOnce(token?: string | null, signal?: AbortSignal): Promise<SocialPostPayload[]> {
  const timeoutController = new AbortController();
  const abortFromCaller = () => timeoutController.abort();
  if (signal?.aborted) timeoutController.abort();
  else signal?.addEventListener('abort', abortFromCaller, { once: true });

  const timeoutId = setTimeout(() => timeoutController.abort(), 8000);
  try {
    const response = await fetchApi(`${apiUrl}/api/mobile/social/posts`, {
      headers: {
        Accept: 'application/json',
        'Cache-Control': 'no-cache',
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      },
      signal: timeoutController.signal,
    });

    if (!response.ok) throw new Error(`PatiSosyal akışı yüklenemedi (${response.status}).`);
    return response.json();
  } catch (error) {
    if (signal?.aborted) throw error;
    if (timeoutController.signal.aborted) {
      throw new Error('PetWork sunucusu zamanında yanıt vermedi. Wi-Fi bağlantını kontrol edip yeniden dene.');
    }
    throw error;
  } finally {
    clearTimeout(timeoutId);
    signal?.removeEventListener('abort', abortFromCaller);
  }
}

export async function getSocialPosts(token?: string | null, signal?: AbortSignal): Promise<SocialPostPayload[]> {
  try {
    return await fetchSocialPostsOnce(token, signal);
  } catch (firstError) {
    if (signal?.aborted) throw firstError;
    await new Promise(resolve => setTimeout(resolve, 500));
    return fetchSocialPostsOnce(token, signal);
  }
}

export async function getQuestions(signal?: AbortSignal): Promise<QuestionsResponse> {
  const response = await fetchApi(`${apiUrl}/api/mobile/questions?take=50`, {
    headers: { Accept: 'application/json' }, signal,
  });
  if (!response.ok) throw new Error(`Sorular alınamadı (${response.status}).`);
  return response.json();
}

export async function getNearbyVeterinarians(
  latitude: number,
  longitude: number,
  radiusMeters = 5000,
): Promise<NearbyVeterinarian[]> {
  const response = await fetchApi(`${apiUrl}/api/mobile/nearby/veterinarians`, {
    method: 'POST',
    headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
    body: JSON.stringify({ latitude, longitude, radiusMeters }),
  });
  const payload = await response.json().catch(() => null) as NearbyVeterinarian[] | { message?: string } | null;
  if (!response.ok) {
    const errorPayload = payload as { message?: string } | null;
    throw new Error(errorPayload?.message || `Veterinerler alınamadı (${response.status}).`);
  }
  return payload as NearbyVeterinarian[];
}

export async function searchVeterinariansByArea(
  city?: string,
  district?: string,
): Promise<NearbyVeterinarian[]> {
  const query = new URLSearchParams();
  if (city?.trim()) query.set('city', city.trim());
  if (district?.trim()) query.set('district', district.trim());
  const response = await fetchApi(`${apiUrl}/api/mobile/nearby/veterinarians/search?${query}`, {
    headers: { Accept: 'application/json' },
  });
  const payload = await response.json().catch(() => null) as NearbyVeterinarian[] | { message?: string } | null;
  if (!response.ok) {
    const errorPayload = payload as { message?: string } | null;
    throw new Error(errorPayload?.message || `Veterinerler alınamadı (${response.status}).`);
  }
  return payload as NearbyVeterinarian[];
}

export async function getNearbyGroomers(
  latitude: number,
  longitude: number,
  radiusMeters = 5000,
): Promise<NearbyVeterinarian[]> {
  const response = await fetchApi(`${apiUrl}/api/mobile/nearby/groomers`, {
    method: 'POST',
    headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
    body: JSON.stringify({ latitude, longitude, radiusMeters }),
  });
  const payload = await response.json().catch(() => null) as NearbyVeterinarian[] | { message?: string } | null;
  if (!response.ok) {
    const errorPayload = payload as { message?: string } | null;
    throw new Error(errorPayload?.message || `Pet kuaförleri alınamadı (${response.status}).`);
  }
  return payload as NearbyVeterinarian[];
}

export async function searchGroomersByArea(
  city?: string,
  district?: string,
): Promise<NearbyVeterinarian[]> {
  const query = new URLSearchParams();
  if (city?.trim()) query.set('city', city.trim());
  if (district?.trim()) query.set('district', district.trim());
  const response = await fetchApi(`${apiUrl}/api/mobile/nearby/groomers/search?${query}`, {
    headers: { Accept: 'application/json' },
  });
  const payload = await response.json().catch(() => null) as NearbyVeterinarian[] | { message?: string } | null;
  if (!response.ok) {
    const errorPayload = payload as { message?: string } | null;
    throw new Error(errorPayload?.message || `Pet kuaförleri alınamadı (${response.status}).`);
  }
  return payload as NearbyVeterinarian[];
}

export async function getNearbyPetHotels(
  latitude: number,
  longitude: number,
  radiusMeters = 5000,
): Promise<NearbyVeterinarian[]> {
  const response = await fetchApi(`${apiUrl}/api/mobile/nearby/pet-hotels`, {
    method: 'POST',
    headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
    body: JSON.stringify({ latitude, longitude, radiusMeters }),
  });
  const payload = await response.json().catch(() => null) as NearbyVeterinarian[] | { message?: string } | null;
  if (!response.ok) {
    const errorPayload = payload as { message?: string } | null;
    throw new Error(errorPayload?.message || `Pet otelleri alınamadı (${response.status}).`);
  }
  return payload as NearbyVeterinarian[];
}

export async function searchPetHotelsByArea(
  city?: string,
  district?: string,
): Promise<NearbyVeterinarian[]> {
  const query = new URLSearchParams();
  if (city?.trim()) query.set('city', city.trim());
  if (district?.trim()) query.set('district', district.trim());
  const response = await fetchApi(`${apiUrl}/api/mobile/nearby/pet-hotels/search?${query}`, {
    headers: { Accept: 'application/json' },
  });
  const payload = await response.json().catch(() => null) as NearbyVeterinarian[] | { message?: string } | null;
  if (!response.ok) {
    const errorPayload = payload as { message?: string } | null;
    throw new Error(errorPayload?.message || `Pet otelleri alınamadı (${response.status}).`);
  }
  return payload as NearbyVeterinarian[];
}

export async function getQuestionDetail(id: number, signal?: AbortSignal): Promise<QuestionDetail> {
  const response = await fetchApi(`${apiUrl}/api/mobile/questions/${id}`, {
    headers: { Accept: 'application/json' }, signal,
  });
  if (!response.ok) throw new Error(`Soru ayrıntısı alınamadı (${response.status}).`);
  return response.json();
}

export async function createQuestion(
  token: string,
  title: string,
  content: string,
  category: string,
): Promise<QuestionSummary> {
  const response = await fetchApi(`${apiUrl}/api/mobile/questions`, {
    method: 'POST',
    headers: {
      Accept: 'application/json',
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify({ title, content, category }),
  });

  const payload = await response.json().catch(() => null) as {
    message?: string;
    title?: string;
    errors?: Record<string, string[]>;
  } | QuestionSummary | null;

  if (!response.ok) {
    const validationMessage = payload && 'errors' in payload && payload.errors
      ? Object.values(payload.errors).flat()[0]
      : undefined;
    const message = payload && 'message' in payload ? payload.message : undefined;
    const responseTitle = payload && 'title' in payload ? payload.title : undefined;
    throw new Error(validationMessage || message || responseTitle || `Soru gönderilemedi (${response.status}).`);
  }

  return payload as QuestionSummary;
}

export async function postQuestionAnswer(token: string, questionId: number, content: string): Promise<QuestionAnswer> {
  const response = await fetchApi(`${apiUrl}/api/mobile/questions/${questionId}/answers`, {
    method: 'POST',
    headers: { Accept: 'application/json', 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
    body: JSON.stringify({ content }),
  });
  const payload = await response.json().catch(() => null) as {
    message?: string; title?: string; errors?: Record<string, string[]>;
  } | QuestionAnswer | null;

  if (!response.ok) {
    const errorPayload = payload as { message?: string; title?: string; errors?: Record<string, string[]> } | null;
    const validationMessage = errorPayload?.errors ? Object.values(errorPayload.errors).flat()[0] : undefined;
    if (response.status === 429) throw new Error('Çok fazla yanıt gönderildi. Lütfen bir dakika sonra tekrar dene.');
    throw new Error(validationMessage || errorPayload?.message || errorPayload?.title || 'Yanıt gönderilemedi.');
  }

  return payload as QuestionAnswer;
}

export async function createSocialPost(
  token: string,
  body: string,
  tags: string[],
  image?: { uri: string; fileName?: string | null; mimeType?: string | null },
): Promise<SocialPostPayload> {
  const imageFile = image ? new File(image.uri) : null;
  const imageBase64 = imageFile ? await imageFile.base64() : null;

  const response = await fetchApi(`${apiUrl}/api/mobile/social/posts`, {
    method: 'POST',
    headers: {
      Accept: 'application/json',
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify({
      body,
      tags: tags.join(','),
      imageBase64,
      imageContentType: image?.mimeType || imageFile?.type || null,
    }),
  });

  const payload = await response.json().catch(() => null) as {
    message?: string;
    title?: string;
    errors?: Record<string, string[]>;
  } | SocialPostPayload | null;

  if (!response.ok) {
    const validationMessage = payload && 'errors' in payload && payload.errors
      ? Object.values(payload.errors).flat()[0]
      : undefined;
    const message = payload && 'message' in payload ? payload.message : undefined;
    const title = payload && 'title' in payload ? payload.title : undefined;
    throw new Error(validationMessage || message || title || `Paylaşım gönderilemedi (${response.status}).`);
  }

  return payload as SocialPostPayload;
}

export async function getSocialComments(postId: number, token?: string | null, signal?: AbortSignal): Promise<SocialCommentPayload[]> {
  const response = await fetchApi(`${apiUrl}/api/mobile/social/posts/${postId}/comments`, {
    headers: { Accept: 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    signal,
  });

  if (!response.ok) throw new Error(`Yorumlar yüklenemedi (${response.status}).`);
  return response.json();
}

export async function createSocialComment(
  token: string,
  postId: number,
  body: string,
): Promise<SocialCommentPayload> {
  const response = await fetchApi(`${apiUrl}/api/mobile/social/posts/${postId}/comments`, {
    method: 'POST',
    headers: {
      Accept: 'application/json',
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify({ body }),
  });

  const payload = await response.json().catch(() => null) as {
    message?: string;
    title?: string;
    errors?: Record<string, string[]>;
  } | SocialCommentPayload | null;

  if (!response.ok) {
    const validationMessage = payload && 'errors' in payload && payload.errors
      ? Object.values(payload.errors).flat()[0]
      : undefined;
    const message = payload && 'message' in payload ? payload.message : undefined;
    const title = payload && 'title' in payload ? payload.title : undefined;
    throw new Error(validationMessage || message || title || `Yorum gönderilemedi (${response.status}).`);
  }

  return payload as SocialCommentPayload;
}

async function readLostPetResponse<T>(response: Response, fallback: string): Promise<T> {
  const payload = await response.json().catch(() => null) as T | { message?: string; title?: string; errors?: Record<string, string[]> } | null;
  if (!response.ok) {
    const error = payload as { message?: string; title?: string; errors?: Record<string, string[]> } | null;
    const validation = error?.errors ? Object.values(error.errors).flat()[0] : undefined;
    throw new ApiError(validation || error?.message || error?.title || fallback, response.status);
  }
  return payload as T;
}

export async function getLostPets(options: {
  kind?: 'lost' | 'found'; city?: string; district?: string; latitude?: number; longitude?: number; radiusKm?: number;
} = {}, signal?: AbortSignal): Promise<LostPetSummary[]> {
  const query = new URLSearchParams();
  Object.entries(options).forEach(([key, value]) => { if (value !== undefined && value !== '') query.set(key, String(value)); });
  const response = await fetchApi(`${apiUrl}/api/mobile/lost-pets${query.size ? `?${query}` : ''}`, { headers: { Accept: 'application/json' }, signal });
  return readLostPetResponse(response, `İlanlar yüklenemedi (${response.status}).`);
}

export async function getMyLostPets(token: string, signal?: AbortSignal): Promise<LostPetSummary[]> {
  const response = await fetchApi(`${apiUrl}/api/mobile/lost-pets/mine`, { headers: { Accept: 'application/json', Authorization: `Bearer ${token}` }, signal });
  return readLostPetResponse(response, `İlanların yüklenemedi (${response.status}).`);
}

export async function getLostPet(id: number, token?: string | null, signal?: AbortSignal): Promise<LostPetDetail> {
  const response = await fetchApi(`${apiUrl}/api/mobile/lost-pets/${id}`, {
    headers: { Accept: 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) }, signal,
  });
  return readLostPetResponse(response, `İlan yüklenemedi (${response.status}).`);
}

export async function createLostPet(token: string, request: CreateLostPetRequest): Promise<LostPetDetail> {
  const imageFile = new File(request.image.uri);
  const response = await fetchApi(`${apiUrl}/api/mobile/lost-pets`, {
    method: 'POST',
    headers: { Accept: 'application/json', 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
    body: JSON.stringify({
      ...request,
      image: undefined,
      imageBase64: await imageFile.base64(),
      imageContentType: request.image.mimeType || imageFile.type || 'image/jpeg',
    }),
  }, 30_000);
  return readLostPetResponse(response, `İlan oluşturulamadı (${response.status}).`);
}

export async function createLostPetSighting(token: string, id: number, request: {
  locationLabel: string; seenAt: string; note?: string; latitude?: number; longitude?: number;
}): Promise<LostPetSighting> {
  const response = await fetchApi(`${apiUrl}/api/mobile/lost-pets/${id}/sightings`, {
    method: 'POST',
    headers: { Accept: 'application/json', 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
    body: JSON.stringify(request),
  });
  return readLostPetResponse(response, `Görülme bildirimi gönderilemedi (${response.status}).`);
}

export async function updateLostPetStatus(token: string, id: number, status: 'active' | 'resolved' | 'closed'): Promise<void> {
  const response = await fetchApi(`${apiUrl}/api/mobile/lost-pets/${id}/status`, {
    method: 'PUT',
    headers: { Accept: 'application/json', 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
    body: JSON.stringify({ status }),
  });
  if (!response.ok) await readLostPetResponse(response, 'İlan durumu güncellenemedi.');
}

export async function deleteLostPet(token: string, id: number): Promise<void> {
  const response = await fetchApi(`${apiUrl}/api/mobile/lost-pets/${id}`, {
    method: 'DELETE', headers: { Accept: 'application/json', Authorization: `Bearer ${token}` },
  });
  if (!response.ok) await readLostPetResponse(response, 'İlan silinemedi.');
}

async function setSocialReaction(path: string, token: string, active: boolean): Promise<SocialReactionPayload> {
  const response = await fetchApi(`${apiUrl}/api/mobile/social/posts/${path}`, {
    method: 'PUT',
    headers: {
      Accept: 'application/json',
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify({ active }),
  });
  const payload = await response.json().catch(() => null) as SocialReactionPayload | { message?: string } | null;
  if (!response.ok) {
    const message = payload && 'message' in payload ? payload.message : undefined;
    throw new ApiError(message || `İşlem tamamlanamadı (${response.status}).`, response.status);
  }
  return payload as SocialReactionPayload;
}

export function setSocialPostLike(token: string, postId: number, active: boolean) {
  return setSocialReaction(`${postId}/like`, token, active);
}

export function setSocialPostSave(token: string, postId: number, active: boolean) {
  return setSocialReaction(`${postId}/save`, token, active);
}

export function setSocialCommentLike(token: string, commentId: number, active: boolean) {
  return setSocialReaction(`comments/${commentId}/like`, token, active);
}

export async function deleteSocialPost(token: string, postId: number): Promise<void> {
  const response = await fetchApi(`${apiUrl}/api/mobile/social/posts/${postId}`, {
    method: 'DELETE',
    headers: { Accept: 'application/json', Authorization: `Bearer ${token}` },
  });

  if (!response.ok) {
    const payload = await response.json().catch(() => null) as { message?: string } | null;
    throw new Error(payload?.message || `Gönderi silinemedi (${response.status}).`);
  }
}

export async function reportSocialPost(token: string, postId: number, reason: string): Promise<string> {
  const response = await fetchApi(`${apiUrl}/api/mobile/social/posts/${postId}/report`, {
    method: 'POST',
    headers: {
      Accept: 'application/json',
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify({ reason }),
  });

  const payload = await response.json().catch(() => null) as { message?: string } | null;
  if (!response.ok) throw new Error(payload?.message || `Gönderi bildirilemedi (${response.status}).`);
  return payload?.message || 'Bildirimin alındı.';
}

async function readPatiMatchResponse<T>(response: Response, fallback: string): Promise<T> {
  const payload = await response.json().catch(() => null) as T | { message?: string; title?: string; errors?: Record<string, string[]> } | null;
  if (!response.ok) {
    const error = payload as { message?: string; title?: string; errors?: Record<string, string[]> } | null;
    const validation = error?.errors ? Object.values(error.errors).flat()[0] : undefined;
    throw new ApiError(validation || error?.message || error?.title || fallback, response.status);
  }
  return payload as T;
}

export async function getPatiMatchOverview(token: string, signal?: AbortSignal): Promise<PatiMatchOverview> {
  const response = await fetch(`${apiUrl}/api/mobile/pati-match`, { headers: { Accept: 'application/json', Authorization: `Bearer ${token}` }, signal });
  return readPatiMatchResponse<PatiMatchOverview>(response, `PatiMatch bilgileri alınamadı (${response.status}).`);
}

export async function getLocationSuggestions(input: string, kind: 'city' | 'district', city?: string, signal?: AbortSignal): Promise<LocationSuggestion[]> {
  const query = new URLSearchParams({ input: input.trim(), kind });
  if (city?.trim()) query.set('city', city.trim());
  const response = await fetch(`${apiUrl}/api/mobile/nearby/locations/autocomplete?${query}`, { headers: { Accept: 'application/json' }, signal });
  return readPatiMatchResponse<LocationSuggestion[]>(response, `Konum önerileri alınamadı (${response.status}).`);
}

export async function enrollPatiMatch(token: string, request: { petId: number; purpose: 'friendship' | 'mate'; city: string; district?: string; preferredTypes: string[]; acceptSafetyTerms: boolean }): Promise<PatiMatchMyPet> {
  const response = await fetch(`${apiUrl}/api/mobile/pati-match/enroll`, {
    method: 'POST',
    headers: { Accept: 'application/json', 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
    body: JSON.stringify(request),
  });
  return readPatiMatchResponse<PatiMatchMyPet>(response, `PatiMatch profili oluşturulamadı (${response.status}).`);
}

export async function getPatiMatchCandidates(token: string, petId: number, signal?: AbortSignal): Promise<PatiMatchCandidate[]> {
  const response = await fetch(`${apiUrl}/api/mobile/pati-match/candidates?petId=${petId}`, { headers: { Accept: 'application/json', Authorization: `Bearer ${token}` }, signal });
  return readPatiMatchResponse<PatiMatchCandidate[]>(response, `PatiMatch adayları alınamadı (${response.status}).`);
}

export async function decidePatiMatch(token: string, sourcePetId: number, targetPetId: number, isLike: boolean): Promise<{ matched: boolean }> {
  const response = await fetch(`${apiUrl}/api/mobile/pati-match/decisions`, {
    method: 'POST',
    headers: { Accept: 'application/json', 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
    body: JSON.stringify({ sourcePetId, targetPetId, isLike }),
  });
  return readPatiMatchResponse<{ matched: boolean }>(response, 'PatiMatch seçimi kaydedilemedi.');
}

export async function getPatiMatches(token: string, petId: number, signal?: AbortSignal): Promise<PatiMatchCandidate[]> {
  const response = await fetch(`${apiUrl}/api/mobile/pati-match/matches?petId=${petId}`, { headers: { Accept: 'application/json', Authorization: `Bearer ${token}` }, signal });
  return readPatiMatchResponse<PatiMatchCandidate[]>(response, `Eşleşmeler alınamadı (${response.status}).`);
}

export async function getPatiMatchMessages(token: string, sourcePetId: number, targetPetId: number, signal?: AbortSignal): Promise<PatiMatchMessage[]> {
  const query = new URLSearchParams({ sourcePetId: String(sourcePetId), targetPetId: String(targetPetId) });
  const response = await fetch(`${apiUrl}/api/mobile/pati-match/messages?${query}`, {
    headers: { Accept: 'application/json', Authorization: `Bearer ${token}` }, signal,
  });
  return readPatiMatchResponse<PatiMatchMessage[]>(response, `Mesajlar alınamadı (${response.status}).`);
}

export async function sendPatiMatchMessage(token: string, sourcePetId: number, targetPetId: number, body: string): Promise<PatiMatchMessage> {
  const response = await fetch(`${apiUrl}/api/mobile/pati-match/messages`, {
    method: 'POST',
    headers: { Accept: 'application/json', 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
    body: JSON.stringify({ sourcePetId, targetPetId, body }),
  });
  return readPatiMatchResponse<PatiMatchMessage>(response, `Mesaj gönderilemedi (${response.status}).`);
}

export async function deactivatePatiMatch(token: string, petId: number): Promise<void> {
  const response = await fetch(`${apiUrl}/api/mobile/pati-match/profiles/${petId}`, {
    method: 'DELETE', headers: { Accept: 'application/json', Authorization: `Bearer ${token}` },
  });
  if (!response.ok) await readPatiMatchResponse(response, 'PatiMatch profili kapatılamadı.');
}
